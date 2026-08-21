namespace Korp.Integration.Contracts.StockDeduction.V1;

public sealed record StockDeductionRequestItemV1(Guid ProductId, int Quantity);
