using Korp.Billing.Domain;
using Korp.Billing.Domain.Invoices;

namespace Korp.Billing.UnitTests.Invoices;

public sealed class InvoiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TstData010CreateProducesOpenUnlockedInvoice()
    {
        var invoice = CreateInvoice();

        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.False(invoice.IsIssuanceInProgress);
        Assert.Null(invoice.ClosedAtUtc);
        Assert.Empty(invoice.Items);
    }

    [Fact]
    public void TstData010CreateRejectsInvalidRequiredValues()
    {
        Assert.Throws<DomainRuleException>(() => Invoice.Create(Guid.Empty, 1, Guid.NewGuid(), Now));
        Assert.Throws<DomainRuleException>(() => Invoice.Create(Guid.NewGuid(), 0, Guid.NewGuid(), Now));
        Assert.Throws<DomainRuleException>(() => Invoice.Create(Guid.NewGuid(), 1, Guid.Empty, Now));
        Assert.Throws<DomainRuleException>(() => Invoice.Create(Guid.NewGuid(), 1, Guid.NewGuid(), default));
    }

    [Fact]
    public void TstData010ItemsCanBeAddedUpdatedAndRemovedWhileEditable()
    {
        var invoice = CreateInvoice();
        var itemId = Guid.NewGuid();

        invoice.AddItem(itemId, Guid.NewGuid(), " p-1 ", " Product ", 2, Now.AddMinutes(1));
        invoice.UpdateItemQuantity(itemId, 3, Now.AddMinutes(2));

        var item = Assert.Single(invoice.Items);
        Assert.Equal("P-1", item.ProductCode);
        Assert.Equal("Product", item.ProductDescription);
        Assert.Equal(3, item.Quantity);

        invoice.RemoveItem(itemId, Now.AddMinutes(3));
        Assert.Empty(invoice.Items);
    }

    [Fact]
    public void TstData010ItemsRejectDuplicateProductInvalidSnapshotQuantityAndUnknownItem()
    {
        var invoice = CreateInvoice();
        var productId = Guid.NewGuid();
        invoice.AddItem(Guid.NewGuid(), productId, "P1", "Product", 1, Now.AddMinutes(1));

        Assert.Throws<DomainRuleException>(() => invoice.AddItem(Guid.NewGuid(), productId, "P1", "Product", 1, Now.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(() => invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), " ", "Product", 1, Now.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(() => invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P2", "Product", 0, Now.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(() => invoice.UpdateItemQuantity(Guid.NewGuid(), 2, Now.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(() => invoice.RemoveItem(Guid.NewGuid(), Now.AddMinutes(2)));
    }

    [Fact]
    public void TstData010IssuanceBlocksEditingAndCompletionClosesPermanently()
    {
        var invoice = CreateInvoiceWithItem();

        invoice.StartIssuance(Now.AddMinutes(2));
        Assert.True(invoice.IsIssuanceInProgress);
        Assert.Throws<DomainRuleException>(() => invoice.UpdateItemQuantity(invoice.Items.Single().Id, 2, Now.AddMinutes(3)));

        invoice.CompleteIssuance(Now.AddMinutes(4));

        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.False(invoice.IsIssuanceInProgress);
        Assert.Equal(Now.AddMinutes(4), invoice.ClosedAtUtc);
        Assert.Throws<DomainRuleException>(() => invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P2", "Product", 1, Now.AddMinutes(5)));
    }

    [Fact]
    public void TstData010IssuanceRejectionUnlocksAndManualInterventionKeepsBlocked()
    {
        var rejectedInvoice = CreateInvoiceWithItem();
        rejectedInvoice.StartIssuance(Now.AddMinutes(2));
        rejectedInvoice.RejectIssuance(Now.AddMinutes(3));
        Assert.False(rejectedInvoice.IsIssuanceInProgress);

        var blockedInvoice = CreateInvoiceWithItem();
        blockedInvoice.StartIssuance(Now.AddMinutes(2));
        blockedInvoice.KeepBlockedForManualIntervention(Now.AddMinutes(3));
        Assert.True(blockedInvoice.IsIssuanceInProgress);
    }

    [Fact]
    public void TstData010IssuanceRejectsEmptyInvoiceMissingProcessAndBackwardTimestamp()
    {
        var emptyInvoice = CreateInvoice();
        Assert.Throws<DomainRuleException>(() => emptyInvoice.StartIssuance(Now.AddMinutes(1)));

        var invoice = CreateInvoiceWithItem();
        Assert.Throws<DomainRuleException>(() => invoice.CompleteIssuance(Now.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(() => invoice.UpdateItemQuantity(invoice.Items.Single().Id, 2, Now.AddSeconds(-1)));
    }

    private static Invoice CreateInvoice() => Invoice.Create(Guid.NewGuid(), 1, Guid.NewGuid(), Now);

    private static Invoice CreateInvoiceWithItem()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P1", "Product", 1, Now.AddMinutes(1));
        return invoice;
    }
}
