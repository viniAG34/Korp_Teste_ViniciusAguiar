using Korp.Inventory.Domain;
using Korp.Inventory.Domain.Products;

namespace Korp.Inventory.UnitTests.Products;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TstData004CreateNormalizesAndPersistsValidState()
    {
        var authorId = Guid.NewGuid();

        var product = Product.Create(Guid.NewGuid(), " product.01 ", " Product description ", 10, authorId, Now);

        Assert.Equal("PRODUCT.01", product.Code.Value);
        Assert.Equal("Product description", product.Description);
        Assert.Equal(10, product.Balance);
        Assert.Equal(authorId, product.CreatedByUserId);
        Assert.Equal(Now, product.CreatedAtUtc);
        Assert.Equal(Now, product.UpdatedAtUtc);
    }

    [Fact]
    public void TstData004CreateRejectsInvalidIdentityAuthorTimestampDescriptionAndBalance()
    {
        Assert.Throws<DomainRuleException>(() => Product.Create(Guid.Empty, "P1", "Product", 0, Guid.NewGuid(), Now));
        Assert.Throws<DomainRuleException>(() => Product.Create(Guid.NewGuid(), "P1", "Product", 0, Guid.Empty, Now));
        Assert.Throws<DomainRuleException>(() => Product.Create(Guid.NewGuid(), "P1", "Product", 0, Guid.NewGuid(), default));
        Assert.Throws<DomainRuleException>(() => Product.Create(Guid.NewGuid(), "P1", " ", 0, Guid.NewGuid(), Now));
        Assert.Throws<DomainRuleException>(() => Product.Create(Guid.NewGuid(), "P1", new string('A', 201), 0, Guid.NewGuid(), Now));
        Assert.Throws<DomainRuleException>(() => Product.Create(Guid.NewGuid(), "P1", "Product", -1, Guid.NewGuid(), Now));
    }

    [Fact]
    public void TstData004DeductForInvoiceUpdatesBalanceAndCreatesConsistentMovement()
    {
        var product = Product.Create(Guid.NewGuid(), "P1", "Product", 10, Guid.NewGuid(), Now);
        var invoiceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var movement = product.DeductForInvoice(Guid.NewGuid(), invoiceId, eventId, 3, Now.AddMinutes(1));

        Assert.Equal(7, product.Balance);
        Assert.Equal(10, movement.BalanceBefore);
        Assert.Equal(7, movement.BalanceAfter);
        Assert.Equal(3, movement.Quantity);
        Assert.Equal(invoiceId, movement.InvoiceId);
        Assert.Equal(eventId, movement.EventId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void TstData004DeductForInvoiceRejectsInvalidQuantityOrInsufficientBalance(int quantity)
    {
        var product = Product.Create(Guid.NewGuid(), "P1", "Product", 10, Guid.NewGuid(), Now);

        Assert.Throws<DomainRuleException>(() =>
            product.DeductForInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity, Now.AddMinutes(1)));
    }
}
