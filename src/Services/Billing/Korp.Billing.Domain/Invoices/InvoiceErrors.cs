namespace Korp.Billing.Domain.Invoices;

public static class InvoiceErrors
{
    public const string InvalidId = "invoice_invalid_id";
    public const string InvalidNumber = "invoice_invalid_number";
    public const string InvalidAuthor = "invoice_invalid_author";
    public const string InvalidTimestamp = "invoice_invalid_timestamp";
    public const string NotOpen = "invoice_not_open";
    public const string IssuanceInProgress = "invoice_issuance_in_progress";
    public const string IssuanceNotInProgress = "invoice_issuance_not_in_progress";
    public const string Empty = "invoice_empty";
    public const string ProductAlreadyAdded = "invoice_product_already_added";
    public const string ItemNotFound = "invoice_item_not_found";
    public const string InvalidQuantity = "invoice_item_quantity_invalid";
    public const string InvalidProductSnapshot = "invoice_product_snapshot_invalid";
}
