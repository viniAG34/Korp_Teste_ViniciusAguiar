namespace Korp.Integration.Contracts.StockDeduction.V1;

public sealed record StockDeductionRequestedV1(
    Guid IssuanceProcessId,
    Guid InvoiceId,
    long InvoiceNumber,
    Guid RequestedByUserId,
    IReadOnlyList<StockDeductionRequestItemV1> Items);
