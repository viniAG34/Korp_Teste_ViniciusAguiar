namespace Korp.Integration.Contracts.StockDeduction.V1;

public sealed record StockDeductionCompletedV1(Guid IssuanceProcessId, Guid InvoiceId);
