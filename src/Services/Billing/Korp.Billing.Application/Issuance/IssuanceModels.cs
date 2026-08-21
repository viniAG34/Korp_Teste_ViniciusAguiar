using Korp.Billing.Domain.Issuance;

namespace Korp.Billing.Application.Issuance;

public sealed record IssuanceProcessDetails(
    Guid Id,
    Guid InvoiceId,
    InvoiceIssuanceProcessStatus Status,
    bool IsDelayed,
    int? RetryAfterSeconds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? OutcomeCode,
    string? OutcomeDescription,
    uint InvoiceVersion);

public sealed record PersistedIssuanceProcess(
    Guid Id,
    Guid InvoiceId,
    Guid IdempotencyKey,
    InvoiceIssuanceProcessStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? OutcomeCode,
    string? OutcomeDescription,
    uint InvoiceVersion);

public static class IssuanceMapping
{
    public static IssuanceProcessDetails ToDetails(this PersistedIssuanceProcess process, DateTimeOffset now)
    {
        var active = process.Status is InvoiceIssuanceProcessStatus.Pending or InvoiceIssuanceProcessStatus.AwaitingStock;
        return new IssuanceProcessDetails(
            process.Id, process.InvoiceId, process.Status,
            active && now - process.UpdatedAtUtc > TimeSpan.FromSeconds(5),
            active ? (now - process.CreatedAtUtc < TimeSpan.FromSeconds(10) ? 1 : 3) : null,
            process.CreatedAtUtc, process.UpdatedAtUtc, process.FinishedAtUtc,
            process.OutcomeCode, process.OutcomeDescription, process.InvoiceVersion);
    }
}
