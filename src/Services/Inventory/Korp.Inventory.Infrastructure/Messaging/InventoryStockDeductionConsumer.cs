using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Korp.Inventory.Infrastructure.Messaging;

public sealed class RabbitMqDeliveryForwarder(
    IRabbitMqConnection connection,
    IOptions<PublisherOptions> publisherOptions) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IChannel? channel;

    public async Task ForwardAsync(BasicDeliverEventArgs delivery, bool deterministic, CancellationToken cancellationToken)
    {
        var currentRetry = HeaderInt(delivery.BasicProperties.Headers, "x-retry-count");
        var deadLetter = deterministic || currentRetry >= 3;
        var routingKey = deadLetter
            ? "inventory.stock-deduction.dead.v1"
            : currentRetry switch
            {
                0 => "inventory.stock-deduction.retry.5s.v1",
                1 => "inventory.stock-deduction.retry.30s.v1",
                _ => "inventory.stock-deduction.retry.120s.v1"
            };
        var exchange = deadLetter ? RabbitMqTopology.DeadLetterExchange : RabbitMqTopology.RetryExchange;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(publisherOptions.Value.ConfirmTimeoutSeconds));
        await gate.WaitAsync(timeout.Token);
        try
        {
            channel = await EnsureChannelAsync(timeout.Token);
            var headers = delivery.BasicProperties.Headers is null
                ? new Dictionary<string, object?>()
                : delivery.BasicProperties.Headers.ToDictionary(pair => pair.Key, pair => pair.Value);
            headers["x-retry-count"] = deadLetter ? currentRetry : currentRetry + 1;
            headers["x-original-queue"] = RabbitMqTopology.InventoryQueue;
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = delivery.BasicProperties.ContentType,
                ContentEncoding = delivery.BasicProperties.ContentEncoding,
                MessageId = delivery.BasicProperties.MessageId,
                Type = delivery.BasicProperties.Type,
                CorrelationId = delivery.BasicProperties.CorrelationId,
                Headers = headers
            };
            await channel.BasicPublishAsync(exchange, routingKey, true, properties, delivery.Body, timeout.Token);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (channel?.IsOpen == true) return channel;
        if (channel is not null) await channel.DisposeAsync();
        channel = await (await connection.GetAsync(cancellationToken)).CreateChannelAsync(
            new CreateChannelOptions(true, true), cancellationToken);
        return channel;
    }

    internal static int HeaderInt(IDictionary<string, object?>? headers, string name)
    {
        if (headers is null || !headers.TryGetValue(name, out var value) || value is null) return 0;
        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => checked((int)number),
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var number) => number,
            _ => 0
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null) await channel.DisposeAsync();
        gate.Dispose();
    }
}

public sealed partial class InventoryStockDeductionConsumer(
    IRabbitMqConnection connection,
    RabbitMqTopologyState topology,
    IServiceScopeFactory scopeFactory,
    RabbitMqDeliveryForwarder forwarder,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<ConsumerOptions> consumerOptions,
    ILogger<InventoryStockDeductionConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rabbitOptions.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested && !topology.IsIncompatible)
        {
            if (!topology.IsDeclared)
            {
                await Task.Delay(500, stoppingToken);
                continue;
            }

            try
            {
                var current = await connection.GetAsync(stoppingToken);
                await using var channel = await current.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, consumerOptions.Value.PrefetchCount, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                    await HandleAsync(channel, delivery, stoppingToken);
                var tag = await channel.BasicConsumeAsync(RabbitMqTopology.InventoryQueue, false, consumer, stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                await channel.BasicCancelAsync(tag, false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                Log.ConsumerFailed(logger, exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(rabbitOptions.Value.NetworkRecoveryIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var headers = delivery.BasicProperties.Headers;
            var request = new StockDeductionDelivery(delivery.Body,
                delivery.BasicProperties.MessageId, delivery.BasicProperties.Type,
                delivery.BasicProperties.CorrelationId, delivery.BasicProperties.ContentType,
                delivery.BasicProperties.ContentEncoding,
                RabbitMqDeliveryForwarder.HeaderInt(headers, "x-message-version"),
                HeaderString(headers, "x-producer"));
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<StockDeductionMessageProcessor>()
                .ProcessAsync(request, cancellationToken);
            if (result.Outcome == StockDeductionProcessingOutcome.DeterministicFailure)
                await forwarder.ForwardAsync(delivery, true, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
            Log.MessageProcessed(logger, delivery.BasicProperties.MessageId ?? string.Empty, result.Outcome.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            try
            {
                await forwarder.ForwardAsync(delivery, false, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                Log.MessageRetried(logger, delivery.BasicProperties.MessageId ?? string.Empty,
                    RabbitMqDeliveryForwarder.HeaderInt(delivery.BasicProperties.Headers, "x-retry-count") + 1);
            }
            catch
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, false, true, cancellationToken);
                throw;
            }
        }
    }

    private static string? HeaderString(IDictionary<string, object?>? headers, string name)
    {
        if (headers is null || !headers.TryGetValue(name, out var value) || value is null) return null;
        return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value.ToString();
    }

    private static partial class Log
    {
        [LoggerMessage(7201, LogLevel.Information, "Message processed. Event={Event} MessageId={MessageId} Outcome={Outcome}")]
        public static partial void MessageProcessed(ILogger logger, string messageId, string outcome, string @event = "message_processed");
        [LoggerMessage(7202, LogLevel.Warning, "Message scheduled for retry. Event={Event} MessageId={MessageId} Attempt={Attempt}")]
        public static partial void MessageRetried(ILogger logger, string messageId, int attempt, string @event = "message_retried");
        [LoggerMessage(7203, LogLevel.Warning, "Inventory consumer cycle failed. Event={Event} FailureType={FailureType}")]
        public static partial void ConsumerFailed(ILogger logger, string failureType, string @event = "rabbitmq_connection_changed");
    }
}
