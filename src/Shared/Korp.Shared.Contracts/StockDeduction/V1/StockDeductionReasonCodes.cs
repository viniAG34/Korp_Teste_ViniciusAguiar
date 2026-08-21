namespace Korp.Integration.Contracts.StockDeduction.V1;

public static class StockDeductionReasonCodes
{
    public const string InsufficientStock = "insufficient_stock";
    public const string ProductNotFound = "product_not_found";
    public const string InvalidRequest = "invalid_stock_deduction_request";
    public const string ProcessingFailed = "stock_processing_failed";
}
