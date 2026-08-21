namespace Korp.Integration.Contracts.Events;

public sealed record IntegrationEventEnvelope<TPayload>(
    Guid MessageId,
    string MessageType,
    int MessageVersion,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    Guid? CausationId,
    string Producer,
    TPayload Payload);
