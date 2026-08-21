namespace Korp.Billing.Api.Features.Issuance.Contracts;

public sealed record InvoiceIssuanceProcessResponse(
    Guid Id,
    Guid InvoiceId,
    InvoiceIssuanceProcessStatusResponse Status,
    bool IsDelayed,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? FinishedAtUtc = null,
    string? OutcomeCode = null,
    string? OutcomeDescription = null);
