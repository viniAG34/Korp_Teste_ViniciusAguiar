using Korp.Billing.Api.Features.Invoices.Contracts;
using Korp.Billing.Api.Features.Issuance.Contracts;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;

namespace Korp.Billing.Api.Features.Invoices;

internal static class InvoiceResponseMapper
{
    public static InvoiceResponse Map(InvoiceDetails invoice) => new(
        invoice.Id, invoice.Number, Map(invoice.Status), invoice.IsIssuanceInProgress,
        invoice.Items.Select(item => new InvoiceItemResponse(
            item.Id, item.ProductId, item.ProductCode, item.ProductDescription, item.Quantity)).ToArray(),
        invoice.CreatedAtUtc, invoice.UpdatedAtUtc, invoice.ClosedAtUtc);

    public static InvoiceSummaryResponse Map(InvoiceSummary invoice) => new(
        invoice.Id, invoice.Number, Map(invoice.Status), invoice.IsIssuanceInProgress,
        invoice.ItemCount, invoice.CreatedAtUtc, invoice.UpdatedAtUtc);

    public static InvoiceIssuanceProcessResponse Map(IssuanceProcessDetails process) => new(
        process.Id, process.InvoiceId, Map(process.Status), process.IsDelayed,
        process.CreatedAtUtc, process.UpdatedAtUtc, process.FinishedAtUtc,
        process.OutcomeCode, process.OutcomeDescription);

    private static InvoiceStatusResponse Map(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Open => InvoiceStatusResponse.Open,
        InvoiceStatus.Closed => InvoiceStatusResponse.Closed,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static InvoiceIssuanceProcessStatusResponse Map(InvoiceIssuanceProcessStatus status) => status switch
    {
        InvoiceIssuanceProcessStatus.Pending => InvoiceIssuanceProcessStatusResponse.Pending,
        InvoiceIssuanceProcessStatus.AwaitingStock => InvoiceIssuanceProcessStatusResponse.AwaitingStock,
        InvoiceIssuanceProcessStatus.Completed => InvoiceIssuanceProcessStatusResponse.Completed,
        InvoiceIssuanceProcessStatus.Rejected => InvoiceIssuanceProcessStatusResponse.Rejected,
        InvoiceIssuanceProcessStatus.ManualIntervention => InvoiceIssuanceProcessStatusResponse.ManualIntervention,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
