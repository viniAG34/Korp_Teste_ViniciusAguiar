using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using BillingDomain = Korp.Billing.Domain;
using BillingInfrastructure = Korp.Billing.Infrastructure;
using BillingMessaging = Korp.Billing.Infrastructure.Messaging;
using BillingPersistence = Korp.Billing.Infrastructure.Persistence;
using BillingOutbox = Korp.Billing.Infrastructure.Persistence.Messaging.OutboxMessage;
using InventoryDomain = Korp.Inventory.Domain;
using InventoryInfrastructure = Korp.Inventory.Infrastructure;
using InventoryMessaging = Korp.Inventory.Infrastructure.Messaging;
using InventoryPersistence = Korp.Inventory.Infrastructure.Persistence;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Korp.Distributed.IntegrationTests;

public sealed class DistributedIssuanceTests : IAsyncLifetime
{
    private ServiceProvider billing = null!;
    private ServiceProvider inventory = null!;
    private IHostedService[] billingWorkers = [];
    private IHostedService[] inventoryWorkers = [];

    public async ValueTask InitializeAsync()
    {
        var billingServices = new ServiceCollection().AddLogging();
        BillingInfrastructure.DependencyInjection.AddBillingInfrastructure(billingServices,
            Configuration("BillingDatabase", Required("BILLING_TEST_CONNECTION")));
        billing = billingServices.BuildServiceProvider();
        var inventoryServices = new ServiceCollection().AddLogging();
        InventoryInfrastructure.DependencyInjection.AddInventoryInfrastructure(inventoryServices,
            Configuration("InventoryDatabase", Required("INVENTORY_TEST_CONNECTION")));
        inventory = inventoryServices.BuildServiceProvider();

        await ResetDatabasesAsync();
        await using var rabbit = await CreateRabbitConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await BillingMessaging.RabbitMqTopologyInitializer.DeclareAsync(channel, TestContext.Current.CancellationToken);
        foreach (var queue in new[] { BillingMessaging.RabbitMqTopology.InventoryQueue,
            BillingMessaging.RabbitMqTopology.BillingQueue,
            BillingMessaging.RabbitMqTopology.InventoryDeadLetterQueue,
            BillingMessaging.RabbitMqTopology.BillingDeadLetterQueue })
            await channel.QueuePurgeAsync(queue, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TstDst006And025CompleteFlowAndRecoverPublishBeforeLocalConfirmation()
    {
        var now = DateTimeOffset.UtcNow;
        var productId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using (var scope = inventory.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>();
            context.Products.Add(InventoryDomain.Products.Product.Create(
                productId, "DIST-001", "Distributed product", 5, userId, now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = billing.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>();
            var invoice = BillingDomain.Invoices.Invoice.Create(invoiceId, 9001, userId, now);
            invoice.AddItem(Guid.NewGuid(), productId, "DIST-001", "Distributed product", 2, now);
            invoice.StartIssuance(now);
            context.Invoices.Add(invoice);
            context.InvoiceIssuanceProcesses.Add(BillingDomain.Issuance.InvoiceIssuanceProcess.Create(
                processId, invoiceId, Guid.NewGuid(), userId, now));
            var payload = new StockDeductionRequestedV1(processId, invoiceId, 9001, userId,
                [new StockDeductionRequestItemV1(productId, 2)]);
            var envelope = new IntegrationEventEnvelope<StockDeductionRequestedV1>(messageId,
                IntegrationEventTypes.StockDeductionRequested, 1, now, correlationId, null,
                IntegrationEventProducers.Billing, payload);
            context.OutboxMessages.Add(BillingOutbox.Create(messageId,
                IntegrationEventTypes.StockDeductionRequested, 1,
                JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web), correlationId, null, now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        inventoryWorkers = inventory.GetServices<IHostedService>().ToArray();
        billingWorkers = billing.GetServices<IHostedService>().ToArray();
        foreach (var worker in inventoryWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);
        foreach (var worker in billingWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);

        await WaitUntilAsync(async () =>
        {
            await using var scope = billing.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>()
                .InvoiceIssuanceProcesses.AnyAsync(process => process.Id == processId
                    && process.Status == BillingDomain.Issuance.InvoiceIssuanceProcessStatus.Completed,
                    TestContext.Current.CancellationToken);
        });

        await using var billingScope = billing.CreateAsyncScope();
        await using var inventoryScope = inventory.CreateAsyncScope();
        var billingDb = billingScope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>();
        var inventoryDb = inventoryScope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>();
        Assert.Equal(BillingDomain.Invoices.InvoiceStatus.Closed,
            await billingDb.Invoices.Where(invoice => invoice.Id == invoiceId)
                .Select(invoice => invoice.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, await inventoryDb.Products.Where(product => product.Id == productId)
            .Select(product => product.Balance).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Single(await inventoryDb.StockMovements.Where(movement => movement.InvoiceId == invoiceId)
            .ToListAsync(TestContext.Current.CancellationToken));

        await billingDb.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE outbox_messages
            SET published_at_utc = NULL, next_attempt_at_utc = {{DateTimeOffset.UtcNow}},
                lock_id = NULL, locked_until_utc = NULL
            WHERE id = {{messageId}}
            """, TestContext.Current.CancellationToken);
        await WaitUntilAsync(async () =>
        {
            await using var scope = billing.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>()
                .OutboxMessages.AnyAsync(message => message.Id == messageId && message.PublishedAtUtc != null,
                    TestContext.Current.CancellationToken);
        });
        billingDb.ChangeTracker.Clear();
        inventoryDb.ChangeTracker.Clear();
        Assert.Single(await inventoryDb.StockMovements.Where(movement => movement.InvoiceId == invoiceId)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await inventoryDb.InboxMessages.Where(message => message.MessageId == messageId)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstDst015RetryQueueReturnsOriginalMessageAfterConfiguredTtl()
    {
        await using var rabbit = await CreateRabbitConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(
            new CreateChannelOptions(true, true), TestContext.Current.CancellationToken);
        await channel.QueuePurgeAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
            TestContext.Current.CancellationToken);
        var messageId = Guid.NewGuid().ToString("D");
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId,
            Type = IntegrationEventTypes.StockDeductionRequested,
            Headers = new Dictionary<string, object?> { ["x-retry-count"] = 1 }
        };
        var started = DateTimeOffset.UtcNow;
        await channel.BasicPublishAsync(BillingMessaging.RabbitMqTopology.RetryExchange,
            "inventory.stock-deduction.retry.5s.v1", true, properties, "{}"u8.ToArray(),
            TestContext.Current.CancellationToken);

        BasicGetResult? returned = null;
        var deadline = started.AddSeconds(10);
        while (returned is null && DateTimeOffset.UtcNow < deadline)
        {
            returned = await channel.BasicGetAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
                true, TestContext.Current.CancellationToken);
            if (returned is null) await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.NotNull(returned);
        Assert.Equal(messageId, returned!.BasicProperties.MessageId);
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromSeconds(4.5));
        Assert.Equal([5_000, 30_000, 120_000],
            BillingMessaging.RabbitMqTopology.RetryQueues.Take(3).Select(queue => queue.TtlMilliseconds));
    }

    [Fact]
    public async Task TstDst017FailedRetryForwardKeepsOriginalDeliveryRecoverable()
    {
        await using var rabbit = await CreateRabbitConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(
            new CreateChannelOptions(true, true), TestContext.Current.CancellationToken);
        await channel.QueuePurgeAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
            TestContext.Current.CancellationToken);
        var body = "original-body"u8.ToArray();
        var properties = new BasicProperties { Persistent = true, MessageId = Guid.NewGuid().ToString("D") };
        await channel.BasicPublishAsync(BillingMessaging.RabbitMqTopology.BillingExchange,
            BillingMessaging.RabbitMqTopology.RequestRoutingKey, true, properties, body,
            TestContext.Current.CancellationToken);
        var original = await channel.BasicGetAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
            false, TestContext.Current.CancellationToken);
        Assert.NotNull(original);

        const string retryQueue = "korp.inventory.stock-deduction.retry-5s.v1";
        const string retryRoutingKey = "inventory.stock-deduction.retry.5s.v1";
        await channel.QueueUnbindAsync(retryQueue, BillingMessaging.RabbitMqTopology.RetryExchange,
            retryRoutingKey, cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            await Assert.ThrowsAsync<PublishReturnException>(() => channel.BasicPublishAsync(
                BillingMessaging.RabbitMqTopology.RetryExchange, retryRoutingKey, true,
                properties, original!.Body, TestContext.Current.CancellationToken).AsTask());
            await channel.BasicNackAsync(original!.DeliveryTag, false, true, TestContext.Current.CancellationToken);
        }
        finally
        {
            await channel.QueueBindAsync(retryQueue, BillingMessaging.RabbitMqTopology.RetryExchange,
                retryRoutingKey, cancellationToken: TestContext.Current.CancellationToken);
        }

        var recovered = await channel.BasicGetAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
            true, TestContext.Current.CancellationToken);
        Assert.NotNull(recovered);
        Assert.Equal(body, recovered!.Body.ToArray());
        Assert.Equal(original!.BasicProperties.MessageId, recovered.BasicProperties.MessageId);
    }

    [Fact]
    public async Task TstDst021ShutdownDuringInventoryProcessingRollsBackAndRedelivers()
    {
        var now = DateTimeOffset.UtcNow;
        var productId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        await using (var seedScope = inventory.CreateAsyncScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>();
            context.Products.Add(InventoryDomain.Products.Product.Create(
                productId, "STOP-001", "Shutdown product", 5, Guid.NewGuid(), now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var lockScope = inventory.CreateAsyncScope();
        var lockContext = lockScope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>();
        await using var transaction = await lockContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await lockContext.Database.ExecuteSqlRawAsync("LOCK TABLE products IN ACCESS EXCLUSIVE MODE",
            TestContext.Current.CancellationToken);

        inventoryWorkers = inventory.GetServices<IHostedService>().ToArray();
        foreach (var worker in inventoryWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => Task.FromResult(inventory.GetRequiredService<InventoryMessaging.MessagingOperationalState>()
            .IsConsumerRunning));

        var envelope = new IntegrationEventEnvelope<StockDeductionRequestedV1>(messageId,
            IntegrationEventTypes.StockDeductionRequested, 1, now, correlationId, null,
            IntegrationEventProducers.Billing, new StockDeductionRequestedV1(
                Guid.NewGuid(), Guid.NewGuid(), 9101, Guid.NewGuid(), [new(productId, 2)]));
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonSerializerOptions.Web);
        await using var rabbit = await CreateRabbitConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(new CreateChannelOptions(true, true),
            TestContext.Current.CancellationToken);
        var properties = new BasicProperties
        {
            Persistent = true, ContentType = "application/json", ContentEncoding = "utf-8",
            MessageId = messageId.ToString("D"), Type = IntegrationEventTypes.StockDeductionRequested,
            CorrelationId = correlationId.ToString("D"), Headers = new Dictionary<string, object?>
            {
                ["x-message-version"] = 1, ["x-producer"] = IntegrationEventProducers.Billing,
                ["x-retry-count"] = 0
            }
        };
        await channel.BasicPublishAsync(BillingMessaging.RabbitMqTopology.BillingExchange,
            BillingMessaging.RabbitMqTopology.RequestRoutingKey, true, properties, body,
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(async () =>
            (await channel.QueueDeclarePassiveAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
                TestContext.Current.CancellationToken)).MessageCount == 0);

        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var worker in inventoryWorkers.Reverse()) await worker.StopAsync(shutdown.Token);
        inventoryWorkers = [];
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        await WaitUntilAsync(async () =>
            (await channel.QueueDeclarePassiveAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
                TestContext.Current.CancellationToken)).MessageCount == 1);
        await using var verificationScope = inventory.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>();
        Assert.Empty(await verification.InboxMessages.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.StockMovements.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(5, await verification.Products.Where(product => product.Id == productId)
            .Select(product => product.Balance).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstDst020FlowRecoversWhenInventoryStartsAfterRequestWasPublished()
    {
        var scenario = await SeedIssuanceAsync("LATE-INV", 9201);
        billingWorkers = billing.GetServices<IHostedService>().ToArray();
        foreach (var worker in billingWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);

        await using var rabbit = await CreateRabbitConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await WaitUntilAsync(async () =>
            (await channel.QueueDeclarePassiveAsync(BillingMessaging.RabbitMqTopology.InventoryQueue,
                TestContext.Current.CancellationToken)).MessageCount == 1);

        inventoryWorkers = inventory.GetServices<IHostedService>().ToArray();
        foreach (var worker in inventoryWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);

        await AssertIssuanceCompletedAsync(scenario.ProcessId, scenario.InvoiceId, scenario.ProductId, 3);
    }

    [Fact]
    public async Task TstDst020FlowRecoversWhenBillingStartsAfterResultWasPublished()
    {
        var scenario = await SeedIssuanceAsync("LATE-BIL", 9202, includeOutbox: false);
        inventoryWorkers = inventory.GetServices<IHostedService>().ToArray();
        foreach (var worker in inventoryWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);

        await PublishRequestAsync(scenario);
        await using var rabbit = await CreateRabbitConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await WaitUntilAsync(async () =>
            (await channel.QueueDeclarePassiveAsync(BillingMessaging.RabbitMqTopology.BillingQueue,
                TestContext.Current.CancellationToken)).MessageCount == 1);

        billingWorkers = billing.GetServices<IHostedService>().ToArray();
        foreach (var worker in billingWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);

        await AssertIssuanceCompletedAsync(scenario.ProcessId, scenario.InvoiceId, scenario.ProductId, 3);
    }

    [Fact]
    public async Task TstDst020FlowRecoversAfterBrokerClosesServiceConnections()
    {
        inventoryWorkers = inventory.GetServices<IHostedService>().ToArray();
        billingWorkers = billing.GetServices<IHostedService>().ToArray();
        foreach (var worker in inventoryWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);
        foreach (var worker in billingWorkers) await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => Task.FromResult(
            inventory.GetRequiredService<InventoryMessaging.IRabbitMqConnection>().IsOpen
            && billing.GetRequiredService<BillingMessaging.IRabbitMqConnection>().IsOpen));

        await CloseServiceBrokerConnectionsAsync();
        var scenario = await SeedIssuanceAsync("BROKER-REC", 9203);

        await AssertIssuanceCompletedAsync(scenario.ProcessId, scenario.InvoiceId, scenario.ProductId, 3);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var worker in billingWorkers.Reverse()) await worker.StopAsync(CancellationToken.None);
        foreach (var worker in inventoryWorkers.Reverse()) await worker.StopAsync(CancellationToken.None);
        await billing.DisposeAsync();
        await inventory.DisposeAsync();
    }

    private async Task ResetDatabasesAsync()
    {
        await using (var scope = billing.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE invoice_items, invoice_issuance_processes, invoices, inbox_messages, outbox_messages CASCADE",
                TestContext.Current.CancellationToken);
        }
        await using var inventoryScope = inventory.CreateAsyncScope();
        var inventoryDb = inventoryScope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>();
        await inventoryDb.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await inventoryDb.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE stock_movements, products, inbox_messages, outbox_messages CASCADE",
            TestContext.Current.CancellationToken);
    }

    private async Task<IssuanceScenario> SeedIssuanceAsync(string sku, int number, bool includeOutbox = true)
    {
        var scenario = new IssuanceScenario(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), number, DateTimeOffset.UtcNow);
        await using (var scope = inventory.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>();
            context.Products.Add(InventoryDomain.Products.Product.Create(
                scenario.ProductId, sku, "Recovery product", 5, scenario.UserId, scenario.OccurredAtUtc));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = billing.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>();
            var invoice = BillingDomain.Invoices.Invoice.Create(
                scenario.InvoiceId, number, scenario.UserId, scenario.OccurredAtUtc);
            invoice.AddItem(Guid.NewGuid(), scenario.ProductId, sku, "Recovery product", 2,
                scenario.OccurredAtUtc);
            invoice.StartIssuance(scenario.OccurredAtUtc);
            context.Invoices.Add(invoice);
            context.InvoiceIssuanceProcesses.Add(BillingDomain.Issuance.InvoiceIssuanceProcess.Create(
                scenario.ProcessId, scenario.InvoiceId, Guid.NewGuid(), scenario.UserId, scenario.OccurredAtUtc));
            if (includeOutbox)
            {
                var envelope = CreateRequestEnvelope(scenario);
                context.OutboxMessages.Add(BillingOutbox.Create(scenario.MessageId,
                    IntegrationEventTypes.StockDeductionRequested, 1,
                    JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web), scenario.CorrelationId, null,
                    scenario.OccurredAtUtc));
            }
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        return scenario;
    }

    private static IntegrationEventEnvelope<StockDeductionRequestedV1> CreateRequestEnvelope(
        IssuanceScenario scenario) => new(scenario.MessageId, IntegrationEventTypes.StockDeductionRequested, 1,
        scenario.OccurredAtUtc, scenario.CorrelationId, null, IntegrationEventProducers.Billing,
        new StockDeductionRequestedV1(scenario.ProcessId, scenario.InvoiceId, scenario.Number, scenario.UserId,
            [new StockDeductionRequestItemV1(scenario.ProductId, 2)]));

    private static async Task PublishRequestAsync(IssuanceScenario scenario)
    {
        await using var rabbit = await CreateRabbitConnectionAsync();
        await using var channel = await rabbit.CreateChannelAsync(
            new CreateChannelOptions(true, true), TestContext.Current.CancellationToken);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = scenario.MessageId.ToString("D"),
            Type = IntegrationEventTypes.StockDeductionRequested,
            CorrelationId = scenario.CorrelationId.ToString("D"),
            Headers = new Dictionary<string, object?>
            {
                ["x-message-version"] = 1,
                ["x-producer"] = IntegrationEventProducers.Billing,
                ["x-retry-count"] = 0
            }
        };
        await channel.BasicPublishAsync(BillingMessaging.RabbitMqTopology.BillingExchange,
            BillingMessaging.RabbitMqTopology.RequestRoutingKey, true, properties,
            JsonSerializer.SerializeToUtf8Bytes(CreateRequestEnvelope(scenario), JsonSerializerOptions.Web),
            TestContext.Current.CancellationToken);
    }

    private static async Task CloseServiceBrokerConnectionsAsync()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{Required("RABBITMQ_TEST_HOST")}:{Required("RABBITMQ_TEST_MANAGEMENT_PORT")}")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{Required("RABBITMQ_TEST_USERNAME")}:{Required("RABBITMQ_TEST_PASSWORD")}")));
        await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("/api/connections", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var connections = await JsonDocument.ParseAsync(content,
            cancellationToken: TestContext.Current.CancellationToken);
        var serviceConnections = connections.RootElement.EnumerateArray()
            .Where(connection => connection.TryGetProperty("client_properties", out var properties)
                && properties.TryGetProperty("connection_name", out var name)
                && name.GetString() is "korp-billing" or "korp-inventory")
            .Select(connection => connection.GetProperty("name").GetString())
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();
        Assert.Equal(2, serviceConnections.Length);
        foreach (var name in serviceConnections)
        {
            using var closed = await client.DeleteAsync($"/api/connections/{Uri.EscapeDataString(name)}",
                TestContext.Current.CancellationToken);
            closed.EnsureSuccessStatusCode();
        }
    }

    private async Task AssertIssuanceCompletedAsync(Guid processId, Guid invoiceId, Guid productId,
        decimal expectedBalance)
    {
        await WaitUntilAsync(async () =>
        {
            await using var scope = billing.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>()
                .InvoiceIssuanceProcesses.AnyAsync(process => process.Id == processId
                    && process.Status == BillingDomain.Issuance.InvoiceIssuanceProcessStatus.Completed,
                    TestContext.Current.CancellationToken);
        });
        await using var billingScope = billing.CreateAsyncScope();
        await using var inventoryScope = inventory.CreateAsyncScope();
        Assert.Equal(BillingDomain.Invoices.InvoiceStatus.Closed,
            await billingScope.ServiceProvider.GetRequiredService<BillingPersistence.BillingDbContext>()
                .Invoices.Where(invoice => invoice.Id == invoiceId).Select(invoice => invoice.Status)
                .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedBalance,
            await inventoryScope.ServiceProvider.GetRequiredService<InventoryPersistence.InventoryDbContext>()
                .Products.Where(product => product.Id == productId).Select(product => product.Balance)
                .SingleAsync(TestContext.Current.CancellationToken));
    }

    private static IConfiguration Configuration(string databaseName, string connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{databaseName}"] = connectionString,
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
        }).Build();

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.True(await condition());
    }

    private static Task<IConnection> CreateRabbitConnectionAsync() => new ConnectionFactory
    {
        HostName = Required("RABBITMQ_TEST_HOST"),
        Port = int.Parse(Required("RABBITMQ_TEST_PORT"), System.Globalization.CultureInfo.InvariantCulture),
        VirtualHost = Required("RABBITMQ_TEST_VHOST"),
        UserName = Required("RABBITMQ_TEST_USERNAME"),
        Password = Required("RABBITMQ_TEST_PASSWORD")
    }.CreateConnectionAsync(TestContext.Current.CancellationToken);

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is required.");

    private sealed record IssuanceScenario(Guid ProductId, Guid InvoiceId, Guid ProcessId, Guid MessageId,
        Guid CorrelationId, Guid UserId, int Number, DateTimeOffset OccurredAtUtc);
}
