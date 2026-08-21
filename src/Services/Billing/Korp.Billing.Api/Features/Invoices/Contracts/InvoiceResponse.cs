namespace Korp.Billing.Api.Features.Invoices.Contracts;

public sealed record InvoiceResponse(
    Guid Id,
    long Number,
    InvoiceStatusResponse Status,
    bool IsIssuanceInProgress,
    IReadOnlyList<InvoiceItemResponse> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClosedAtUtc = null);
