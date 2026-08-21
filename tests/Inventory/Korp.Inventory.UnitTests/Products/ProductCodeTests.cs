using Korp.Inventory.Domain;
using Korp.Inventory.Domain.Products;

namespace Korp.Inventory.UnitTests.Products;

public sealed class ProductCodeTests
{
    [Theory]
    [InlineData(" product-01 ", "PRODUCT-01")]
    [InlineData("abc_def.2", "ABC_DEF.2")]
    public void TstData004CreateNormalizesValidCode(string input, string expected)
    {
        var code = ProductCode.Create(input);

        Assert.Equal(expected, code.Value);
        Assert.Equal(expected, code.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("INVALID CODE")]
    [InlineData("INVALID@CODE")]
    public void TstData004CreateRejectsInvalidCode(string input)
    {
        Assert.Throws<DomainRuleException>(() => ProductCode.Create(input));
    }

    [Fact]
    public void TstData004CreateRejectsCodeLongerThanFiftyCharacters()
    {
        var exception = Assert.Throws<DomainRuleException>(() => ProductCode.Create(new string('A', 51)));

        Assert.Equal(ProductErrors.CodeTooLong, exception.Code);
    }
}
