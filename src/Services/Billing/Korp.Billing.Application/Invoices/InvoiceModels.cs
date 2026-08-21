using Korp.Billing.Domain.Invoices;

namespace Korp.Billing.Application.Invoices;

public sealed record InvoiceItemDetails(
    Guid Id, Guid ProductId, string ProductCode, string ProductDescription, int Quantity);

public sealed record InvoiceDetails(
    Guid Id,
    long Number,
    InvoiceStatus Status,
    bool IsIssuanceInProgress,
    IReadOnlyList<InvoiceItemDetails> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    uint Version);

public sealed record InvoiceSummary(
    Guid Id,
    long Number,
    InvoiceStatus Status,
    bool IsIssuanceInProgress,
    int ItemCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record InvoicePage(
    IReadOnlyList<InvoiceSummary> Items,
    int PageNumber,
    int PageSize,
    long TotalCount,
    int TotalPages);

public static class InvoiceMapping
{
    public static InvoiceDetails ToDetails(this Invoice invoice) => new(
        invoice.Id,
        invoice.Number,
        invoice.Status,
        invoice.IsIssuanceInProgress,
        invoice.Items.OrderBy(item => item.ProductCode, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .Select(item => new InvoiceItemDetails(
                item.Id, item.ProductId, item.ProductCode, item.ProductDescription, item.Quantity))
            .ToArray(),
        invoice.CreatedAtUtc,
        invoice.UpdatedAtUtc,
        invoice.ClosedAtUtc,
        invoice.Version);
}
