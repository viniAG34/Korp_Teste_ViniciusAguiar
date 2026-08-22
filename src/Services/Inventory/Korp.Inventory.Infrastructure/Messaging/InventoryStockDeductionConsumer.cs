using System.Diagnostics;
using System.Text;
using Korp.Inventory.Application.Stock;
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

    public async Task ForwardAsync(BasicDeliverEventArgs delivery, int currentRetry, bool deterministic,
        string errorCode, CancellationToken cancellationToken)
    {
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
            if (deadLetter)
            {
                headers["x-original-exchange"] = delivery.Exchange;
                headers["x-original-routing-key"] = delivery.RoutingKey;
                headers["x-error-code"] = errorCode;
                headers["x-failed-at-utc"] = DateTimeOffset.UtcNow.ToString("O");
                headers["x-failed-consumer"] = "inventory";
            }
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

    internal static bool TryRetryCount(IDictionary<string, object?>? headers, out int count)
    {
        count = 0;
        if (headers is null || !headers.TryGetValue("x-retry-count", out var value) || value is null) return true;
        try
        {
            count = value switch
            {
                byte number => number, short number => number, int number => number,
                long number => checked((int)number),
                byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var number) => number,
                _ => -1
            };
        }
        catch (OverflowException) { count = -1; }
        return count is >= 0 and <= 3;
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null) await channel.DisposeAsync();
        gate.Dispose();
    }
}

public sealed partial class InventoryStockDeductionConsumer(
    IRabbitMqConnection connection,
    MessagingOperationalState state,
    IServiceScopeFactory scopeFactory,
    RabbitMqDeliveryForwarder forwarder,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<ConsumerOptions> consumerOptions,
    MessagingMetrics metrics,
    ILogger<InventoryStockDeductionConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rabbitOptions.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested && !state.IsTopologyIncompatible)
        {
            if (!state.IsTopologyDeclared)
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
                state.SetConsumerRunning(true);
                try
                {
                    while (channel.IsOpen)
                        await Task.Delay(250, stoppingToken);
                }
                finally
                {
                    state.SetConsumerRunning(false);
                    if (channel.IsOpen)
                        await channel.BasicCancelAsync(tag, false, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                state.SetConsumerRunning(false);
                Log.ConsumerFailed(logger, exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(rabbitOptions.Value.NetworkRecoveryIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var messageType = delivery.BasicProperties.Type ?? "unknown";
        Log.MessageReceived(logger, delivery.BasicProperties.MessageId ?? string.Empty, messageType);
        var forwarding = false;
        try
        {
            var headers = delivery.BasicProperties.Headers;
            if (!RabbitMqDeliveryForwarder.TryRetryCount(headers, out var retryCount))
            {
                forwarding = true;
                await forwarder.ForwardAsync(delivery, 0, true, "invalid_retry_header", cancellationToken);
                metrics.RecordDeadLetter("invalid_retry_header");
                Log.MessageDeadLettered(logger, delivery.BasicProperties.MessageId ?? string.Empty, "invalid_retry_header");
                forwarding = false;
                await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                return;
            }
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
            {
                forwarding = true;
                await forwarder.ForwardAsync(delivery, retryCount, true,
                    result.FailureCode ?? "deterministic_failure", cancellationToken);
                metrics.RecordDeadLetter(result.FailureCode ?? "deterministic_failure");
                Log.IntegrityViolation(logger, delivery.BasicProperties.MessageId ?? string.Empty,
                    result.FailureCode ?? "deterministic_failure");
                Log.MessageDeadLettered(logger, delivery.BasicProperties.MessageId ?? string.Empty,
                    result.FailureCode ?? "deterministic_failure");
                forwarding = false;
            }
            if (result.Outcome == StockDeductionProcessingOutcome.Duplicate)
            {
                metrics.RecordDuplicate();
                Log.DuplicateIgnored(logger, delivery.BasicProperties.MessageId ?? string.Empty);
            }
            metrics.RecordConsumed(messageType, result.Outcome.ToString(), Stopwatch.GetElapsedTime(started));
            await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
            Log.MessageProcessed(logger, delivery.BasicProperties.MessageId ?? string.Empty, result.Outcome.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            try
            {
                if (forwarding)
                {
                    await Task.Delay(250, cancellationToken);
                    await channel.BasicNackAsync(delivery.DeliveryTag, false, true, cancellationToken);
                    return;
                }
                RabbitMqDeliveryForwarder.TryRetryCount(delivery.BasicProperties.Headers, out var retryCount);
                if (retryCount >= 3)
                {
                    var headers = delivery.BasicProperties.Headers;
                    var request = new StockDeductionDelivery(delivery.Body,
                        delivery.BasicProperties.MessageId, delivery.BasicProperties.Type,
                        delivery.BasicProperties.CorrelationId, delivery.BasicProperties.ContentType,
                        delivery.BasicProperties.ContentEncoding,
                        RabbitMqDeliveryForwarder.HeaderInt(headers, "x-message-version"), HeaderString(headers, "x-producer"));
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var terminal = await scope.ServiceProvider.GetRequiredService<StockDeductionMessageProcessor>()
                        .FinalizeFailureAsync(request, cancellationToken);
                    if (terminal is TerminalFailureStatus.Confirmed or TerminalFailureStatus.AlreadyProcessed)
                    {
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                        return;
                    }
                }
                forwarding = true;
                await forwarder.ForwardAsync(delivery, retryCount, false,
                    retryCount >= 3 ? "processing_outcome_inconclusive" : "transient_processing_failure",
                    cancellationToken);
                if (retryCount >= 3)
                {
                    metrics.RecordDeadLetter("processing_outcome_inconclusive");
                    Log.MessageDeadLettered(logger, delivery.BasicProperties.MessageId ?? string.Empty,
                        "processing_outcome_inconclusive");
                }
                else metrics.RecordRetry(retryCount + 1);
                forwarding = false;
                await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                Log.MessageRetried(logger, delivery.BasicProperties.MessageId ?? string.Empty,
                    retryCount + 1);
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
        [LoggerMessage(7204, LogLevel.Debug, "Message received. Event={Event} MessageId={MessageId} MessageType={MessageType}")]
        public static partial void MessageReceived(ILogger logger, string messageId, string messageType, string @event = "message_received");
        [LoggerMessage(7205, LogLevel.Information, "Duplicate message ignored. Event={Event} MessageId={MessageId}")]
        public static partial void DuplicateIgnored(ILogger logger, string messageId, string @event = "duplicate_message_ignored");
        [LoggerMessage(7206, LogLevel.Warning, "Message integrity violation. Event={Event} MessageId={MessageId} ErrorCode={ErrorCode}")]
        public static partial void IntegrityViolation(ILogger logger, string messageId, string errorCode, string @event = "message_integrity_violation");
        [LoggerMessage(7207, LogLevel.Warning, "Message dead-lettered. Event={Event} MessageId={MessageId} ErrorCode={ErrorCode}")]
        public static partial void MessageDeadLettered(ILogger logger, string messageId, string errorCode, string @event = "message_dead_lettered");
    }
}
