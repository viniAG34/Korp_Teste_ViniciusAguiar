using System.ComponentModel.DataAnnotations;

namespace Korp.Billing.Infrastructure.Messaging;

public sealed class PublisherOptions
{
    public const string SectionName = "Messaging:Publisher";
    [Range(1, 60)] public int ConfirmTimeoutSeconds { get; init; } = 5;
}

public sealed class OutboxOptions
{
    public const string SectionName = "Messaging:Outbox";
    [Range(1, 50)] public int BatchSize { get; init; } = 50;
    [Range(100, 60_000)] public int PollingIntervalMilliseconds { get; init; } = 1_000;
    [Range(5, 600)] public int LeaseSeconds { get; init; } = 60;
}

public sealed class ConsumerOptions
{
    public const string SectionName = "Messaging:Consumer";
    [Range(1, 100)] public ushort PrefetchCount { get; init; } = 1;
}
