namespace Korp.Integration.Contracts.StockDeduction.V1;

public sealed record StockDeductionFailureV1(
    Guid ProductId,
    int RequestedQuantity,
    int? AvailableBalance = null);
