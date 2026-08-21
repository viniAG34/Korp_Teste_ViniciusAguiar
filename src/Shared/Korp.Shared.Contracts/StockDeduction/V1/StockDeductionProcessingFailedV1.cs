namespace Korp.Integration.Contracts.StockDeduction.V1;

public sealed record StockDeductionProcessingFailedV1(
    Guid IssuanceProcessId,
    Guid InvoiceId,
    string ReasonCode,
    string ReasonDescription);
