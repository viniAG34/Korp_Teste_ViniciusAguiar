namespace Korp.Billing.Api.Features.Invoices.Contracts;

public sealed record AddInvoiceItemRequest(Guid ProductId, int Quantity);
