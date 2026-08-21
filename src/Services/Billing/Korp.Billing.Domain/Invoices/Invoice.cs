namespace Korp.Billing.Domain.Invoices;

public sealed class Invoice
{
    private readonly List<InvoiceItem> _items = [];

    private Invoice()
    {
    }

    private Invoice(Guid id, long number, Guid createdByUserId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Number = number;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Status = InvoiceStatus.Open;
    }

    public Guid Id { get; private set; }

    public long Number { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public bool IsIssuanceInProgress { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<InvoiceItem> Items => _items.AsReadOnly();

    public static Invoice Create(Guid id, long number, Guid createdByUserId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidId, "Invoice id is required.");
        }

        if (number <= 0)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidNumber, "Invoice number must be positive.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidAuthor, "Invoice author is required.");
        }

        if (createdAtUtc == default)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidTimestamp, "Invoice creation timestamp is required.");
        }

        return new Invoice(id, number, createdByUserId, createdAtUtc);
    }

    public void AddItem(
        Guid itemId,
        Guid productId,
        string productCode,
        string productDescription,
        int quantity,
        DateTimeOffset updatedAtUtc)
    {
        EnsureEditable();
        if (_items.Any(item => item.ProductId == productId))
        {
            throw new DomainRuleException(InvoiceErrors.ProductAlreadyAdded, "Product is already present in the invoice.");
        }

        _items.Add(InvoiceItem.Create(itemId, Id, productId, productCode, productDescription, quantity));
        Touch(updatedAtUtc);
    }

    public void UpdateItemQuantity(Guid itemId, int quantity, DateTimeOffset updatedAtUtc)
    {
        EnsureEditable();
        var item = FindItem(itemId);
        item.ChangeQuantity(quantity);
        Touch(updatedAtUtc);
    }

    public void RemoveItem(Guid itemId, DateTimeOffset updatedAtUtc)
    {
        EnsureEditable();
        var item = FindItem(itemId);
        _items.Remove(item);
        Touch(updatedAtUtc);
    }

    public void StartIssuance(DateTimeOffset updatedAtUtc)
    {
        EnsureEditable();
        if (_items.Count == 0)
        {
            throw new DomainRuleException(InvoiceErrors.Empty, "Invoice must contain at least one item.");
        }

        IsIssuanceInProgress = true;
        Touch(updatedAtUtc);
    }

    public void CompleteIssuance(DateTimeOffset closedAtUtc)
    {
        EnsureIssuanceInProgress();
        Status = InvoiceStatus.Closed;
        IsIssuanceInProgress = false;
        ClosedAtUtc = closedAtUtc;
        Touch(closedAtUtc);
    }

    public void RejectIssuance(DateTimeOffset updatedAtUtc)
    {
        EnsureIssuanceInProgress();
        IsIssuanceInProgress = false;
        Touch(updatedAtUtc);
    }

    public void KeepBlockedForManualIntervention(DateTimeOffset updatedAtUtc)
    {
        EnsureIssuanceInProgress();
        Touch(updatedAtUtc);
    }

    private void EnsureEditable()
    {
        if (Status != InvoiceStatus.Open)
        {
            throw new DomainRuleException(InvoiceErrors.NotOpen, "Invoice is not open.");
        }

        if (IsIssuanceInProgress)
        {
            throw new DomainRuleException(InvoiceErrors.IssuanceInProgress, "Invoice issuance is already in progress.");
        }
    }

    private void EnsureIssuanceInProgress()
    {
        if (Status != InvoiceStatus.Open)
        {
            throw new DomainRuleException(InvoiceErrors.NotOpen, "Invoice is not open.");
        }

        if (!IsIssuanceInProgress)
        {
            throw new DomainRuleException(InvoiceErrors.IssuanceNotInProgress, "Invoice has no issuance in progress.");
        }
    }

    private InvoiceItem FindItem(Guid itemId) =>
        _items.SingleOrDefault(item => item.Id == itemId)
        ?? throw new DomainRuleException(InvoiceErrors.ItemNotFound, "Invoice item was not found.");

    private void Touch(DateTimeOffset updatedAtUtc)
    {
        if (updatedAtUtc < CreatedAtUtc || updatedAtUtc < UpdatedAtUtc)
        {
            throw new DomainRuleException(InvoiceErrors.InvalidTimestamp, "Invoice timestamp cannot move backwards.");
        }

        UpdatedAtUtc = updatedAtUtc;
    }
}
