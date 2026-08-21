using Korp.Inventory.Domain.StockMovements;

namespace Korp.Inventory.Domain.Products;

public sealed class Product
{
    public const int DescriptionMaxLength = 200;

    private Product()
    {
    }

    private Product(
        Guid id,
        ProductCode code,
        string description,
        int balance,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Code = code;
        Description = description;
        Balance = balance;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public ProductCode Code { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public int Balance { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Product Create(
        Guid id,
        string code,
        string description,
        int initialBalance,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException(ProductErrors.InvalidId, "Product id is required.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainRuleException(ProductErrors.InvalidAuthor, "Product author is required.");
        }

        if (createdAtUtc == default)
        {
            throw new DomainRuleException(ProductErrors.InvalidTimestamp, "Product creation timestamp is required.");
        }

        if (initialBalance < 0)
        {
            throw new DomainRuleException(ProductErrors.BalanceNegative, "Initial balance cannot be negative.");
        }

        return new Product(
            id,
            ProductCode.Create(code),
            NormalizeDescription(description),
            initialBalance,
            createdByUserId,
            createdAtUtc);
    }

    public StockMovement DeductForInvoice(
        Guid movementId,
        Guid invoiceId,
        Guid eventId,
        int quantity,
        DateTimeOffset occurredAtUtc)
    {
        if (quantity <= 0)
        {
            throw new DomainRuleException(ProductErrors.QuantityInvalid, "Deduction quantity must be positive.");
        }

        if (quantity > Balance)
        {
            throw new DomainRuleException(ProductErrors.InsufficientBalance, "Product balance is insufficient.");
        }

        var balanceBefore = Balance;
        Balance -= quantity;
        UpdatedAtUtc = occurredAtUtc;

        return StockMovement.CreateInvoiceDeduction(
            movementId,
            Id,
            invoiceId,
            eventId,
            quantity,
            balanceBefore,
            Balance,
            occurredAtUtc);
    }

    private static string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainRuleException(ProductErrors.DescriptionRequired, "Product description is required.");
        }

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > DescriptionMaxLength)
        {
            throw new DomainRuleException(ProductErrors.DescriptionTooLong, $"Product description cannot exceed {DescriptionMaxLength} characters.");
        }

        if (normalizedDescription.Any(char.IsControl))
        {
            throw new DomainRuleException(
                ProductErrors.DescriptionContainsControlCharacters,
                "Product description cannot contain control characters.");
        }

        return normalizedDescription;
    }
}
