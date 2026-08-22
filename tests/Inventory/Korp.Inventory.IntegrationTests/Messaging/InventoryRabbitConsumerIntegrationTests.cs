using System.Text.Json;
using Korp.Inventory.Domain.Products;
using Korp.Inventory.Infrastructure;
using Korp.Inventory.Infrastructure.Messaging;
using Korp.Inventory.Infrastructure.Persistence;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace Korp.Inventory.IntegrationTests.Messaging;

public sealed class InventoryRabbitConsumerIntegrationTests : IAsyncLifetime
{
    private ServiceProvider provider = null!;
    private IHostedService[] hostedServices = [];
    private readonly string database = Environment.GetEnvironmentVariable("INVENTORY_TEST_CONNECTION")
        ?? throw new InvalidOperationException("INVENTORY_TEST_CONNECTION is required.");

    public async ValueTask InitializeAsync()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:InventoryDatabase"] = database,
            ["Messaging:RabbitMq:Enabled"] = "true",
            ["Messaging:RabbitMq:Host"] = Required("RABBITMQ_TEST_HOST"),
            ["Messaging:RabbitMq:Port"] = Required("RABBITMQ_TEST_PORT"),
            ["Messaging:RabbitMq:VirtualHost"] = Required("RABBITMQ_TEST_VHOST"),
            ["Messaging:RabbitMq:Username"] = Required("RABBITMQ_TEST_USERNAME"),
            ["Messaging:RabbitMq:Password"] = Required("RABBITMQ_TEST_PASSWORD"),
            ["Messaging:RabbitMq:RequestedHeartbeatSeconds"] = "30",
            ["Messaging:RabbitMq:NetworkRecoveryIntervalSeconds"] = "5",
            ["Messaging:Publisher:ConfirmTimeoutSeconds"] = "5",
            ["Messaging:Outbox:BatchSize"] = "50",
            ["Messaging:Outbox:PollingIntervalMilliseconds"] = "100",
            ["Messaging:Outbox:LeaseSeconds"] = "60",
            ["Messaging:Consumer:PrefetchCount"] = "1"
        };
        provider = new ServiceCollection().AddLogging()
            .AddInventoryInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE stock_movements, products, inbox_messages, outbox_messages CASCADE",
                TestContext.Current.CancellationToken);
        }
        await using (var rabbit = await CreateConnectionAsync())
        await using (var channel = await rabbit.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            await RabbitMqTopologyInitializer.DeclareAsync(channel, TestContext.Current.CancellationToken);
            await channel.QueuePurgeAsync(RabbitMqTopology.InventoryQueue, TestContext.Current.CancellationToken);
            await channel.QueuePurgeAsync(RabbitMqTopology.InventoryDeadLetterQueue, TestContext.Current.CancellationToken);
        }
        hostedServices = provider.GetServices<IHostedService>().ToArray();
        foreach (var service in hostedServices) await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => provider.GetRequiredService<MessagingOperationalState>().IsTopologyDeclared);
    }

    [Fact]
    public async Task TstDst016MalformedRetryHeaderGoesDirectlyToDlqWithSafeMetadata()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var body = JsonSerializer.SerializeToUtf8Bytes(new { invalid = true });
        await using var rabbit = await CreateConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(new CreateChannelOptions(true, true), TestContext.Current.CancellationToken);
        var properties = new BasicProperties
        {
            Persistent = true, ContentType = "application/json", ContentEncoding = "utf-8",
            MessageId = messageId.ToString(), Type = IntegrationEventTypes.StockDeductionRequested,
            CorrelationId = correlationId.ToString(), Headers = new Dictionary<string, object?>
            {
                ["x-message-version"] = 1, ["x-producer"] = IntegrationEventProducers.Billing,
                ["x-retry-count"] = "not-a-number"
            }
        };
        await channel.BasicPublishAsync(RabbitMqTopology.BillingExchange, RabbitMqTopology.RequestRoutingKey,
            true, properties, body, TestContext.Current.CancellationToken);

        BasicGetResult? result = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (result is null && DateTime.UtcNow < deadline)
        {
            result = await channel.BasicGetAsync(RabbitMqTopology.InventoryDeadLetterQueue, true, TestContext.Current.CancellationToken);
            if (result is null) await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        Assert.NotNull(result);
        Assert.Equal(body, result!.Body.ToArray());
        Assert.Equal("invalid_retry_header", Header(result.BasicProperties.Headers, "x-error-code"));
        Assert.Equal("inventory", Header(result.BasicProperties.Headers, "x-failed-consumer"));
        Assert.Equal(RabbitMqTopology.BillingExchange, Header(result.BasicProperties.Headers, "x-original-exchange"));
    }

    private static string? Header(IDictionary<string, object?>? headers, string key) =>
        headers?[key] is byte[] bytes ? System.Text.Encoding.UTF8.GetString(bytes) : headers?[key]?.ToString();

    public async ValueTask DisposeAsync()
    {
        foreach (var service in hostedServices.Reverse()) await service.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task TstDst007RabbitDeliveryCommitsStockInboxAndResultOutboxBeforeAck()
    {
        var productId = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            context.Products.Add(Product.Create(productId, "P-1", "Produto", 5, Guid.NewGuid(), DateTimeOffset.UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var envelope = new IntegrationEventEnvelope<StockDeductionRequestedV1>(messageId,
            IntegrationEventTypes.StockDeductionRequested, 1, DateTimeOffset.UtcNow, correlationId, null,
            IntegrationEventProducers.Billing, new StockDeductionRequestedV1(
                Guid.NewGuid(), Guid.NewGuid(), 8001, Guid.NewGuid(), [new(productId, 2)]));
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonSerializerOptions.Web);
        await using (var rabbit = await CreateConnectionAsync())
        await using (var channel = await rabbit.CreateChannelAsync(new CreateChannelOptions(true, true), TestContext.Current.CancellationToken))
        {
            var properties = new BasicProperties
            {
                Persistent = true, ContentType = "application/json", ContentEncoding = "utf-8",
                MessageId = messageId.ToString("D"), Type = IntegrationEventTypes.StockDeductionRequested,
                CorrelationId = correlationId.ToString("D"),
                Headers = new Dictionary<string, object?>
                {
                    ["x-message-version"] = 1,
                    ["x-producer"] = IntegrationEventProducers.Billing,
                    ["x-retry-count"] = 0
                }
            };
            await channel.BasicPublishAsync(RabbitMqTopology.BillingExchange,
                RabbitMqTopology.RequestRoutingKey, true, properties, body, TestContext.Current.CancellationToken);
        }

        await WaitUntilAsync(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().InboxMessages
                .AnyAsync(message => message.MessageId == messageId, TestContext.Current.CancellationToken);
        });
        await using var verificationScope = provider.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(3, await verification.Products.Where(product => product.Id == productId)
            .Select(product => product.Balance).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.StockMovements.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(50);
        Assert.True(condition());
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await condition() && DateTime.UtcNow < deadline) await Task.Delay(50);
        Assert.True(await condition());
    }

    private static Task<IConnection> CreateConnectionAsync() => new ConnectionFactory
    {
        HostName = Required("RABBITMQ_TEST_HOST"),
        Port = int.Parse(Required("RABBITMQ_TEST_PORT"), System.Globalization.CultureInfo.InvariantCulture),
        VirtualHost = Required("RABBITMQ_TEST_VHOST"),
        UserName = Required("RABBITMQ_TEST_USERNAME"),
        Password = Required("RABBITMQ_TEST_PASSWORD")
    }.CreateConnectionAsync(TestContext.Current.CancellationToken);

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required.");
}
