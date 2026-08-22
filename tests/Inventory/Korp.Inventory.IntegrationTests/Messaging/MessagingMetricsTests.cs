using System.Diagnostics.Metrics;
using Korp.Inventory.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace Korp.Inventory.IntegrationTests.Messaging;

public sealed class MessagingMetricsTests
{
    [Fact]
    public void TstDst023PublishesApprovedLowCardinalityMetrics()
    {
        List<string> instruments = [];
        List<string> tags = [];
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name != "Korp.Inventory.Messaging") return;
                instruments.Add(instrument.Name);
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, currentTags, _) => AddTags(currentTags, tags));
        listener.SetMeasurementEventCallback<double>((_, _, currentTags, _) => AddTags(currentTags, tags));
        listener.SetMeasurementEventCallback<int>((_, _, currentTags, _) => AddTags(currentTags, tags));
        listener.Start();

        using var metrics = new MessagingMetrics(new ClosedConnection());
        metrics.UpdateOutboxSnapshot(new OutboxSnapshot(2, 6));
        metrics.RecordPublication("published", TimeSpan.FromMilliseconds(10));
        metrics.RecordConsumed("StockDeductionRequested", "Processed", TimeSpan.FromMilliseconds(20));
        metrics.RecordRetry(1);
        metrics.RecordDeadLetter("invalid_envelope");
        metrics.RecordDuplicate();
        listener.RecordObservableInstruments();

        Assert.Contains("outbox_pending_messages", instruments);
        Assert.Contains("outbox_oldest_pending_age_seconds", instruments);
        Assert.Contains("rabbitmq_connection_state", instruments);
        Assert.Contains("service=inventory", tags);
        Assert.DoesNotContain(tags, tag => tag.Contains("message_id", StringComparison.OrdinalIgnoreCase)
            || tag.Contains("invoice", StringComparison.OrdinalIgnoreCase)
            || tag.Contains("correlation", StringComparison.OrdinalIgnoreCase)
            || tag.Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddTags(ReadOnlySpan<KeyValuePair<string, object?>> source, List<string> target)
    {
        foreach (var tag in source) target.Add($"{tag.Key}={tag.Value}");
    }

    private sealed class ClosedConnection : IRabbitMqConnection
    {
        public bool IsOpen => false;
        public Task<IConnection> GetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
