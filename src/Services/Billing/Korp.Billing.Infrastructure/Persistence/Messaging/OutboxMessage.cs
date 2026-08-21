namespace Korp.Billing.Infrastructure.Persistence.Messaging;

public sealed class OutboxMessage
{
    private OutboxMessage() { }
    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public Guid? CausationId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public Guid? LockId { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }
    public string? LastError { get; private set; }
    public uint Version { get; private set; }

    public static OutboxMessage Create(Guid id, string messageType, int schemaVersion, string payload, Guid correlationId, Guid? causationId, DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (id == Guid.Empty || correlationId == Guid.Empty || schemaVersion <= 0 || occurredAtUtc == default)
        {
            throw new ArgumentException("Outbox message is invalid.");
        }

        return new OutboxMessage { Id = id, MessageType = messageType, SchemaVersion = schemaVersion, Payload = payload, CorrelationId = correlationId, CausationId = causationId, OccurredAtUtc = occurredAtUtc, NextAttemptAtUtc = occurredAtUtc };
    }

    public void AcquireLease(Guid lockId, DateTimeOffset lockedUntilUtc)
    {
        if (lockId == Guid.Empty || lockedUntilUtc <= NextAttemptAtUtc || PublishedAtUtc is not null) throw new InvalidOperationException("Outbox lease is invalid.");
        LockId = lockId;
        LockedUntilUtc = lockedUntilUtc;
    }

    public void RecordFailure(string error, DateTimeOffset nextAttemptAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        AttemptCount++;
        LastError = error.Trim().Length <= 1000 ? error.Trim() : error.Trim()[..1000];
        NextAttemptAtUtc = nextAttemptAtUtc;
        LockId = null;
        LockedUntilUtc = null;
    }

    public void MarkPublished(DateTimeOffset publishedAtUtc)
    {
        if (LockId is null || publishedAtUtc < OccurredAtUtc) throw new InvalidOperationException("Publisher confirmation requires an active lease.");
        PublishedAtUtc = publishedAtUtc;
        LastError = null;
        LockId = null;
        LockedUntilUtc = null;
    }
}
