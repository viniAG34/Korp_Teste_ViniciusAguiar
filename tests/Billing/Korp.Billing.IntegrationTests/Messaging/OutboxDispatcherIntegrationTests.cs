using System.Text;
using System.Text.Json;
using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;
using Korp.Billing.Infrastructure.Messaging;
using Korp.Billing.Infrastructure.Persistence;
using Korp.Billing.Infrastructure.Persistence.Messaging;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Korp.Billing.IntegrationTests.Messaging;

public sealed class OutboxDispatcherIntegrationTests : IAsyncLifetime
{
    private readonly string connectionString = Environment.GetEnvironmentVariable("BILLING_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Test configuration BILLING_TEST_CONNECTION is required.");

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE invoice_items, invoice_issuance_processes, invoices, inbox_messages, outbox_messages CASCADE",
            TestContext.Current.CancellationToken);
        await using var connection = await CreateRabbitConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await RabbitMqTopologyInitializer.DeclareAsync(channel, TestContext.Current.CancellationToken);
        await channel.QueuePurgeAsync(RabbitMqTopology.InventoryQueue, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TstDst005ConcurrentStoresClaimEachMessageOnce()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var setup = CreateContext())
        {
            for (var index = 0; index < 100; index++)
                setup.OutboxMessages.Add(CreateOutbox(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.AddMilliseconds(index)));
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var firstStore = CreateStore();
        var secondStore = CreateStore();
        var claims = await Task.WhenAll(
            firstStore.ClaimAsync(now.AddSeconds(1), TestContext.Current.CancellationToken),
            secondStore.ClaimAsync(now.AddSeconds(1), TestContext.Current.CancellationToken));

        Assert.Equal(100, claims.Sum(claim => claim.Count));
        Assert.Equal(100, claims.SelectMany(claim => claim).Select(message => message.Id).Distinct().Count());
        Assert.Equal(2, claims.SelectMany(claim => claim).Select(message => message.LockId).Distinct().Count());
    }

    [Fact]
    public async Task TstDst004ConfirmedPublishMarksOutboxAndProcessAwaitingStock()
    {
        var now = DateTimeOffset.UtcNow;
        var invoiceId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        await using (var setup = CreateContext())
        {
            var invoice = Invoice.Create(invoiceId, 7001, Guid.NewGuid(), now);
            invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P-001", "Produto", 2, now);
            invoice.StartIssuance(now);
            setup.Invoices.Add(invoice);
            setup.InvoiceIssuanceProcesses.Add(InvoiceIssuanceProcess.Create(
                processId, invoiceId, Guid.NewGuid(), Guid.NewGuid(), now));
            setup.OutboxMessages.Add(CreateOutbox(messageId, processId, invoiceId, now));
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var store = CreateStore();
        var delivery = Assert.Single(await store.ClaimAsync(now.AddSeconds(1), TestContext.Current.CancellationToken));
        await using var rabbitConnection = new RabbitMqConnection(Options.Create(RabbitOptions()));
        await using (var publisher = new RabbitMqOutboxPublisher(
            rabbitConnection, Options.Create(new PublisherOptions { ConfirmTimeoutSeconds = 5 })))
            await publisher.PublishAsync(delivery, TestContext.Current.CancellationToken);
        await store.MarkPublishedAsync(delivery, now.AddSeconds(2), TestContext.Current.CancellationToken);

        await using var verification = CreateContext();
        var outbox = await verification.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);
        var process = await verification.InvoiceIssuanceProcesses.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(outbox.PublishedAtUtc);
        Assert.Null(outbox.LockId);
        Assert.Equal(InvoiceIssuanceProcessStatus.AwaitingStock, process.Status);

        await using var broker = await CreateRabbitConnectionAsync();
        await using var channel = await broker.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        var published = await channel.BasicGetAsync(RabbitMqTopology.InventoryQueue, true, TestContext.Current.CancellationToken);
        Assert.NotNull(published);
        Assert.Equal(messageId.ToString("D"), published.BasicProperties.MessageId);
        Assert.Equal(Encoding.UTF8.GetBytes(delivery.Payload), published.Body.ToArray());
    }

    [Fact]
    public async Task TstDst004MandatoryReturnDoesNotReportPublicationSuccess()
    {
        var now = DateTimeOffset.UtcNow;
        var message = CreateOutbox(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);
        message.AcquireLease(Guid.NewGuid(), now.AddMinutes(1));
        var delivery = new OutboxDelivery(message.Id, message.LockId!.Value, message.MessageType,
            message.SchemaVersion, message.Payload, message.CorrelationId, message.CausationId,
            message.OccurredAtUtc, message.AttemptCount);

        await using var administration = await CreateRabbitConnectionAsync();
        await using var channel = await administration.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await channel.QueueUnbindAsync(RabbitMqTopology.InventoryQueue, RabbitMqTopology.BillingExchange,
            RabbitMqTopology.RequestRoutingKey, cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            await using var rabbitConnection = new RabbitMqConnection(Options.Create(RabbitOptions()));
            await using var publisher = new RabbitMqOutboxPublisher(
                rabbitConnection, Options.Create(new PublisherOptions { ConfirmTimeoutSeconds = 5 }));
            await Assert.ThrowsAsync<PublishReturnException>(() =>
                publisher.PublishAsync(delivery, TestContext.Current.CancellationToken));
        }
        finally
        {
            await channel.QueueBindAsync(RabbitMqTopology.InventoryQueue, RabbitMqTopology.BillingExchange,
                RabbitMqTopology.RequestRoutingKey, cancellationToken: TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(6, 30)]
    [InlineData(20, 30)]
    public void OutboxBackoffMatchesApprovedSequence(int attempt, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), OutboxBackoff.ForAttempt(attempt));

    private BillingOutboxStore CreateStore() => new(new ContextFactory(connectionString),
        Options.Create(new OutboxOptions { BatchSize = 50, LeaseSeconds = 60, PollingIntervalMilliseconds = 1000 }));

    private static OutboxMessage CreateOutbox(Guid messageId, Guid processId, Guid invoiceId, DateTimeOffset now)
    {
        var payload = new StockDeductionRequestedV1(processId, invoiceId, 7001, Guid.NewGuid(),
            [new StockDeductionRequestItemV1(Guid.NewGuid(), 2)]);
        var envelope = new IntegrationEventEnvelope<StockDeductionRequestedV1>(messageId,
            IntegrationEventTypes.StockDeductionRequested, 1, now, Guid.NewGuid(), null,
            IntegrationEventProducers.Billing, payload);
        return OutboxMessage.Create(messageId, IntegrationEventTypes.StockDeductionRequested, 1,
            JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web), envelope.CorrelationId, null, now);
    }

    private BillingDbContext CreateContext() => new(new DbContextOptionsBuilder<BillingDbContext>()
        .UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options);

    private static RabbitMqOptions RabbitOptions() => new()
    {
        Enabled = true,
        Host = Required("RABBITMQ_TEST_HOST"),
        Port = int.Parse(Required("RABBITMQ_TEST_PORT"), System.Globalization.CultureInfo.InvariantCulture),
        VirtualHost = Required("RABBITMQ_TEST_VHOST"),
        Username = Required("RABBITMQ_TEST_USERNAME"),
        Password = Required("RABBITMQ_TEST_PASSWORD")
    };

    private static async Task<IConnection> CreateRabbitConnectionAsync()
    {
        var options = RabbitOptions();
        return await new ConnectionFactory { HostName = options.Host, Port = options.Port,
            VirtualHost = options.VirtualHost, UserName = options.Username, Password = options.Password }
            .CreateConnectionAsync(TestContext.Current.CancellationToken);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Test configuration {name} is required.");

    private sealed class ContextFactory(string connectionString) : IDbContextFactory<BillingDbContext>
    {
        public BillingDbContext CreateDbContext() => new(new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options);
    }
}
