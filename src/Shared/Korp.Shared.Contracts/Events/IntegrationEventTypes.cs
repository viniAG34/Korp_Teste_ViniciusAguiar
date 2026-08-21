namespace Korp.Integration.Contracts.Events;

public static class IntegrationEventTypes
{
    public const string StockDeductionRequested = "stock_deduction_requested";
    public const string StockDeductionCompleted = "stock_deduction_completed";
    public const string StockDeductionRejected = "stock_deduction_rejected";
    public const string StockDeductionProcessingFailed = "stock_deduction_processing_failed";
}
