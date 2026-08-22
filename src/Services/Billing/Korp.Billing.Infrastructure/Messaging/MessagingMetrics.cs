using System.Diagnostics.Metrics;

namespace Korp.Billing.Infrastructure.Messaging;

public sealed class MessagingMetrics : IDisposable
{
    private readonly Meter meter = new("Korp.Billing.Messaging");
    private readonly Counter<long> publications;
    private readonly Histogram<double> publishDuration;
    private readonly Counter<long> consumed;
    private readonly Histogram<double> processingDuration;
    private readonly Counter<long> retries;
    private readonly Counter<long> deadLetters;
    private readonly Counter<long> duplicates;
    private long pendingMessages;
    private long oldestPendingAgeBits;

    public MessagingMetrics(IRabbitMqConnection connection)
    {
        publications = meter.CreateCounter<long>("outbox_publications_total");
        publishDuration = meter.CreateHistogram<double>("outbox_publish_duration_seconds", "s");
        consumed = meter.CreateCounter<long>("messages_consumed_total");
        processingDuration = meter.CreateHistogram<double>("message_processing_duration_seconds", "s");
        retries = meter.CreateCounter<long>("message_retries_total");
        deadLetters = meter.CreateCounter<long>("messages_dead_lettered_total");
        duplicates = meter.CreateCounter<long>("message_duplicates_total");
        meter.CreateObservableGauge("outbox_pending_messages", () => Volatile.Read(ref pendingMessages));
        meter.CreateObservableGauge("outbox_oldest_pending_age_seconds",
            () => BitConverter.Int64BitsToDouble(Volatile.Read(ref oldestPendingAgeBits)), "s");
        meter.CreateObservableGauge("rabbitmq_connection_state",
            () => new Measurement<int>(connection.IsOpen ? 1 : 0,
                new KeyValuePair<string, object?>("service", "billing")));
    }

    public void RecordPublication(string outcome, TimeSpan duration)
    {
        publications.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        publishDuration.Record(duration.TotalSeconds);
    }

    public void UpdateOutboxSnapshot(OutboxSnapshot snapshot)
    {
        Volatile.Write(ref pendingMessages, snapshot.PendingMessages);
        Volatile.Write(ref oldestPendingAgeBits, BitConverter.DoubleToInt64Bits(snapshot.OldestAgeSeconds));
    }

    public void RecordConsumed(string messageType, string outcome, TimeSpan duration)
    {
        consumed.Add(1, new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("outcome", outcome));
        processingDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("message_type", messageType));
    }

    public void RecordRetry(int stage) => retries.Add(1,
        new KeyValuePair<string, object?>("consumer", "billing"),
        new KeyValuePair<string, object?>("retry_stage", stage));
    public void RecordDeadLetter(string reason) => deadLetters.Add(1,
        new KeyValuePair<string, object?>("consumer", "billing"),
        new KeyValuePair<string, object?>("reason", reason));
    public void RecordDuplicate() => duplicates.Add(1,
        new KeyValuePair<string, object?>("consumer", "billing"));
    public void Dispose() => meter.Dispose();
}
