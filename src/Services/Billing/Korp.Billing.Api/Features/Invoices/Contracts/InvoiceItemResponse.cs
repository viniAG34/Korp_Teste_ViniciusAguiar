namespace Korp.Billing.Api.Features.Invoices.Contracts;

public sealed record InvoiceItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductDescription,
    int Quantity);
