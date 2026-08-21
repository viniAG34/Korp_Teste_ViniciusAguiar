namespace Korp.Integration.Contracts.StockDeduction.V1;

public sealed record StockDeductionRejectedV1(
    Guid IssuanceProcessId,
    Guid InvoiceId,
    string ReasonCode,
    string ReasonDescription,
    IReadOnlyList<StockDeductionFailureV1>? Failures = null);
