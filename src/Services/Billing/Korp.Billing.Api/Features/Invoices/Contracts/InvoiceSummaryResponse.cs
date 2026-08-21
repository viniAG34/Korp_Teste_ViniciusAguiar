namespace Korp.Billing.Api.Features.Invoices.Contracts;

public sealed record InvoiceSummaryResponse(
    Guid Id,
    long Number,
    InvoiceStatusResponse Status,
    bool IsIssuanceInProgress,
    int ItemCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
