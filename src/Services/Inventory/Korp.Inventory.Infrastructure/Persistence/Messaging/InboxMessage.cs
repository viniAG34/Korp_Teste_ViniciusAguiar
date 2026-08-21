namespace Korp.Inventory.Infrastructure.Persistence.Messaging;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public Guid MessageId { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid? CausationId { get; private set; }
    public string PayloadHash { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public static InboxMessage Create(Guid messageId, string messageType, int schemaVersion, Guid correlationId, Guid? causationId, string payloadHash, DateTimeOffset processedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash);
        if (messageId == Guid.Empty || correlationId == Guid.Empty || schemaVersion <= 0 || payloadHash.Length != 64 || processedAtUtc == default)
        {
            throw new ArgumentException("Inbox message is invalid.");
        }

        return new InboxMessage { MessageId = messageId, MessageType = messageType, SchemaVersion = schemaVersion, CorrelationId = correlationId, CausationId = causationId, PayloadHash = payloadHash.ToUpperInvariant(), ProcessedAtUtc = processedAtUtc };
    }
}
