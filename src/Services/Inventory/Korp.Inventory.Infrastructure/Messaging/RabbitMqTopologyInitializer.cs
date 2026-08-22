using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Korp.Inventory.Infrastructure.Messaging;

public sealed class RabbitMqTopologyState
{
    public bool IsDeclared { get; internal set; }
    public bool IsIncompatible { get; internal set; }
}

public sealed partial class RabbitMqTopologyInitializer(
    IRabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    RabbitMqTopologyState state,
    ILogger<RabbitMqTopologyInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        while (!stoppingToken.IsCancellationRequested && !state.IsIncompatible)
        {
            try
            {
                var current = await connection.GetAsync(stoppingToken);
                await using var channel = await current.CreateChannelAsync(cancellationToken: stoppingToken);
                await DeclareAsync(channel, stoppingToken);
                state.IsDeclared = true;
                Log.TopologyDeclared(logger);
                return;
            }
            catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == 406)
            {
                state.IsIncompatible = true;
                Log.TopologyIncompatible(logger, exception.ShutdownReason.ReplyCode);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                state.IsDeclared = false;
                Log.ConnectionUnavailable(logger, exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(options.Value.NetworkRecoveryIntervalSeconds), stoppingToken);
            }
        }
    }

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        foreach (var exchange in new[] { RabbitMqTopology.BillingExchange, RabbitMqTopology.InventoryExchange, RabbitMqTopology.RetryExchange, RabbitMqTopology.DeadLetterExchange })
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, true, false, cancellationToken: cancellationToken);

        await DeclareAndBindAsync(channel, RabbitMqTopology.InventoryQueue, RabbitMqTopology.BillingExchange, RabbitMqTopology.RequestRoutingKey, null, cancellationToken);
        await DeclareAndBindAsync(channel, RabbitMqTopology.BillingQueue, RabbitMqTopology.InventoryExchange, RabbitMqTopology.ResultRoutingKey, null, cancellationToken);

        foreach (var retry in RabbitMqTopology.RetryQueues)
        {
            Dictionary<string, object?> arguments = new()
            {
                ["x-message-ttl"] = retry.TtlMilliseconds,
                ["x-dead-letter-exchange"] = retry.ReturnExchange,
                ["x-dead-letter-routing-key"] = retry.ReturnRoutingKey
            };
            await DeclareAndBindAsync(channel, retry.Name, RabbitMqTopology.RetryExchange, retry.RoutingKey, arguments, cancellationToken);
        }

        await DeclareAndBindAsync(channel, RabbitMqTopology.InventoryDeadLetterQueue, RabbitMqTopology.DeadLetterExchange, "inventory.stock-deduction.dead.v1", null, cancellationToken);
        await DeclareAndBindAsync(channel, RabbitMqTopology.BillingDeadLetterQueue, RabbitMqTopology.DeadLetterExchange, "billing.stock-deduction-result.dead.v1", null, cancellationToken);
    }

    private static async Task DeclareAndBindAsync(IChannel channel, string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(queue, true, false, false, arguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, exchange, routingKey, cancellationToken: cancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage(7001, LogLevel.Information, "RabbitMQ topology declared. Event={Event}")]
        public static partial void TopologyDeclared(ILogger logger, string @event = "rabbitmq_topology_declared");
        [LoggerMessage(7002, LogLevel.Critical, "RabbitMQ topology is incompatible. Event={Event} ReplyCode={ReplyCode}")]
        public static partial void TopologyIncompatible(ILogger logger, ushort replyCode, string @event = "rabbitmq_topology_incompatible");
        [LoggerMessage(7003, LogLevel.Warning, "RabbitMQ connection unavailable. Event={Event} FailureType={FailureType}")]
        public static partial void ConnectionUnavailable(ILogger logger, string failureType, string @event = "rabbitmq_connection_changed");
    }
}
