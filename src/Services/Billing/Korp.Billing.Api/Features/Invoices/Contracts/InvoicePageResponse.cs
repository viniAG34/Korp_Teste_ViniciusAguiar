namespace Korp.Billing.Api.Features.Invoices.Contracts;

public sealed record InvoicePageResponse(
    IReadOnlyList<InvoiceSummaryResponse> Items,
    int PageNumber,
    int PageSize,
    long TotalCount,
    int TotalPages);
