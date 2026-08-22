using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Korp.Inventory.Infrastructure.Messaging;

public sealed partial class OutboxDispatcher(
    IOutboxStore store,
    IOutboxPublisher publisher,
    MessagingOperationalState state,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<OutboxOptions> outboxOptions,
    TimeProvider timeProvider,
    MessagingMetrics metrics,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rabbitOptions.Value.Enabled) return;
        state.SetDispatcherRunning(true);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!state.IsTopologyDeclared)
                    {
                        await WaitAsync(stoppingToken);
                        continue;
                    }

                var now = timeProvider.GetUtcNow();
                metrics.UpdateOutboxSnapshot(await store.GetSnapshotAsync(now, stoppingToken));
                var deliveries = await store.ClaimAsync(now, stoppingToken);
                if (deliveries.Count == 0)
                {
                    await WaitAsync(stoppingToken);
                    continue;
                }

                Log.BatchClaimed(logger, deliveries.Count, deliveries[0].LockId);
                foreach (var delivery in deliveries)
                {
                    var started = Stopwatch.GetTimestamp();
                    try
                    {
                        await publisher.PublishAsync(delivery, stoppingToken);
                        await store.MarkPublishedAsync(delivery, timeProvider.GetUtcNow(), stoppingToken);
                        metrics.RecordPublication("published", Stopwatch.GetElapsedTime(started));
                        Log.MessagePublished(logger, delivery.Id, delivery.CorrelationId, delivery.MessageType);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception)
                    {
                        metrics.RecordPublication("failed", Stopwatch.GetElapsedTime(started));
                        var failure = $"publish_failed:{exception.GetType().Name}";
                        await store.RecordFailureAsync(delivery, failure, timeProvider.GetUtcNow(), stoppingToken);
                        Log.PublishFailed(logger, delivery.Id, delivery.CorrelationId, delivery.MessageType,
                            delivery.AttemptCount + 1, exception.GetType().Name);
                    }
                }
            }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception exception)
                {
                    Log.DispatchCycleFailed(logger, exception.GetType().Name);
                    await WaitAsync(stoppingToken);
                }
            }
        }
        finally { state.SetDispatcherRunning(false); }
    }

    private Task WaitAsync(CancellationToken cancellationToken) =>
        Task.Delay(outboxOptions.Value.PollingIntervalMilliseconds, cancellationToken);

    private static partial class Log
    {
        [LoggerMessage(7101, LogLevel.Debug, "Outbox batch claimed. Event={Event} Count={Count} LockId={LockId}")]
        public static partial void BatchClaimed(ILogger logger, int count, Guid lockId, string @event = "outbox_batch_claimed");
        [LoggerMessage(7102, LogLevel.Information, "Outbox message published. Event={Event} MessageId={MessageId} CorrelationId={CorrelationId} MessageType={MessageType}")]
        public static partial void MessagePublished(ILogger logger, Guid messageId, Guid correlationId, string messageType, string @event = "outbox_message_published");
        [LoggerMessage(7103, LogLevel.Warning, "Outbox publication failed. Event={Event} MessageId={MessageId} CorrelationId={CorrelationId} MessageType={MessageType} Attempt={Attempt} FailureType={FailureType}")]
        public static partial void PublishFailed(ILogger logger, Guid messageId, Guid correlationId, string messageType, int attempt, string failureType, string @event = "outbox_publish_failed");
        [LoggerMessage(7104, LogLevel.Warning, "Outbox dispatch cycle failed. Event={Event} FailureType={FailureType}")]
        public static partial void DispatchCycleFailed(ILogger logger, string failureType, string @event = "outbox_publish_failed");
    }
}
