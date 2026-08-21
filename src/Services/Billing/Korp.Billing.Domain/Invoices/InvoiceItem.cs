namespace Korp.Billing.Domain.Invoices;

public sealed class InvoiceItem
{
    public const int ProductCodeMaxLength = 50;
    public const int ProductDescriptionMaxLength = 200;

    private InvoiceItem()
    {
    }

    private InvoiceItem(Guid id, Guid invoiceId, Guid productId, string productCode, string productDescription, int quantity)
    {
        Id = id;
        InvoiceId = invoiceId;
        ProductId = productId;
        ProductCode = productCode;
        ProductDescription = productDescription;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductDescription { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    internal static InvoiceItem Create(
        Guid id,
        Guid invoiceId,
        Guid productId,
        string productCode,
        string productDescription,
        int quantity)
    {
        if (id == Guid.Empty || invoiceId == Guid.Empty || productId == Guid.Empty)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidId, "Invoice item identifiers are required.");
        }

        if (quantity <= 0)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidQuantity, "Invoice item quantity must be positive.");
        }

        var normalizedCode = NormalizeSnapshot(productCode, ProductCodeMaxLength, true);
        var normalizedDescription = NormalizeSnapshot(productDescription, ProductDescriptionMaxLength, false);

        return new InvoiceItem(id, invoiceId, productId, normalizedCode, normalizedDescription, quantity);
    }

    internal void ChangeQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidQuantity, "Invoice item quantity must be positive.");
        }

        Quantity = quantity;
    }

    private static string NormalizeSnapshot(string value, int maxLength, bool uppercase)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleException(InvoiceErrors.InvalidProductSnapshot, "Product snapshot is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidProductSnapshot, "Product snapshot exceeds its maximum length.");
        }

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }
}
