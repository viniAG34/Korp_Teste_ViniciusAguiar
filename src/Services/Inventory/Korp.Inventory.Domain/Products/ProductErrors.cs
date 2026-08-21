namespace Korp.Inventory.Domain.Products;

public static class ProductErrors
{
    public const string InvalidId = "product_invalid_id";
    public const string CodeRequired = "product_code_required";
    public const string CodeTooLong = "product_code_too_long";
    public const string CodeInvalidFormat = "product_code_invalid_format";
    public const string DescriptionRequired = "product_description_required";
    public const string DescriptionTooLong = "product_description_too_long";
    public const string BalanceNegative = "product_balance_negative";
    public const string QuantityInvalid = "stock_quantity_invalid";
    public const string InsufficientBalance = "insufficient_stock";
    public const string InvalidAuthor = "product_invalid_author";
    public const string InvalidTimestamp = "product_invalid_timestamp";
}
