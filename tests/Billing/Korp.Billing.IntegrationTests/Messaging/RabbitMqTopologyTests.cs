using Korp.Billing.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Korp.Billing.IntegrationTests.Messaging;

public sealed class RabbitMqTopologyTests
{
    [Fact]
    public async Task TstDst001DeclaresCompleteTopologyIdempotently()
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        await RabbitMqTopologyInitializer.DeclareAsync(channel, TestContext.Current.CancellationToken);
        await RabbitMqTopologyInitializer.DeclareAsync(channel, TestContext.Current.CancellationToken);

        foreach (var exchange in new[]
        {
            RabbitMqTopology.BillingExchange,
            RabbitMqTopology.InventoryExchange,
            RabbitMqTopology.RetryExchange,
            RabbitMqTopology.DeadLetterExchange
        })
            await channel.ExchangeDeclarePassiveAsync(exchange, TestContext.Current.CancellationToken);

        foreach (var queue in MainAndRetryQueues())
            await channel.QueueDeclarePassiveAsync(queue, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TstDst002RejectsIncompatibleTopologyWithoutRecreatingResource()
    {
        await using var connection = await CreateConnectionAsync();
        await using var declarationChannel = await connection.CreateChannelAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await RabbitMqTopologyInitializer.DeclareAsync(
            declarationChannel,
            TestContext.Current.CancellationToken);

        await using var incompatibleChannel = await connection.CreateChannelAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<OperationInterruptedException>(() =>
            incompatibleChannel.QueueDeclareAsync(
                RabbitMqTopology.InventoryQueue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(exception.ShutdownReason);
        Assert.Equal((ushort)406, exception.ShutdownReason.ReplyCode);

        await using var verificationChannel = await connection.CreateChannelAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await verificationChannel.QueueDeclarePassiveAsync(
            RabbitMqTopology.InventoryQueue,
            TestContext.Current.CancellationToken);
    }

    private static IEnumerable<string> MainAndRetryQueues()
    {
        yield return RabbitMqTopology.InventoryQueue;
        yield return RabbitMqTopology.BillingQueue;
        yield return RabbitMqTopology.InventoryDeadLetterQueue;
        yield return RabbitMqTopology.BillingDeadLetterQueue;
        foreach (var retry in RabbitMqTopology.RetryQueues) yield return retry.Name;
    }

    private static Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = Required("RABBITMQ_TEST_HOST"),
            Port = int.Parse(Required("RABBITMQ_TEST_PORT"), System.Globalization.CultureInfo.InvariantCulture),
            VirtualHost = Required("RABBITMQ_TEST_VHOST"),
            UserName = Required("RABBITMQ_TEST_USERNAME"),
            Password = Required("RABBITMQ_TEST_PASSWORD")
        };
        return factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Test configuration {name} is required.");
}
