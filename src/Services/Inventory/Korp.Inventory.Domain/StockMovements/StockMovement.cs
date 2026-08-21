using Korp.Inventory.Domain.Products;

namespace Korp.Inventory.Domain.StockMovements;

public sealed class StockMovement
{
    private StockMovement()
    {
    }

    private StockMovement(
        Guid id,
        Guid productId,
        Guid invoiceId,
        int quantity,
        int balanceBefore,
        int balanceAfter,
        Guid eventId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        InvoiceId = invoiceId;
        Quantity = quantity;
        BalanceBefore = balanceBefore;
        BalanceAfter = balanceAfter;
        Type = StockMovementType.InvoiceDeduction;
        EventId = eventId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid InvoiceId { get; private set; }

    public int Quantity { get; private set; }

    public int BalanceBefore { get; private set; }

    public int BalanceAfter { get; private set; }

    public StockMovementType Type { get; private set; }

    public Guid EventId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    internal static StockMovement CreateInvoiceDeduction(
        Guid id,
        Guid productId,
        Guid invoiceId,
        Guid eventId,
        int quantity,
        int balanceBefore,
        int balanceAfter,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || productId == Guid.Empty || invoiceId == Guid.Empty || eventId == Guid.Empty)
        {
            throw new DomainRuleException(ProductErrors.InvalidId, "Movement identifiers are required.");
        }

        if (quantity <= 0 || balanceBefore < 0 || balanceAfter < 0 || balanceAfter != balanceBefore - quantity)
        {
            throw new DomainRuleException(ProductErrors.QuantityInvalid, "Movement balances and quantity are inconsistent.");
        }

        if (createdAtUtc == default)
        {
            throw new DomainRuleException(ProductErrors.InvalidTimestamp, "Movement timestamp is required.");
        }

        return new StockMovement(id, productId, invoiceId, quantity, balanceBefore, balanceAfter, eventId, createdAtUtc);
    }
}
