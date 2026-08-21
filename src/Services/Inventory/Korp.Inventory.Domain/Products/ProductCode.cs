using System.Text.RegularExpressions;

namespace Korp.Inventory.Domain.Products;

public readonly partial record struct ProductCode
{
    public const int MaxLength = 50;

    private ProductCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProductCode Create(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainRuleException(ProductErrors.CodeRequired, "Product code is required.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        if (normalizedCode.Length > MaxLength)
        {
            throw new DomainRuleException(ProductErrors.CodeTooLong, $"Product code cannot exceed {MaxLength} characters.");
        }

        if (!AllowedCharacters().IsMatch(normalizedCode))
        {
            throw new DomainRuleException(ProductErrors.CodeInvalidFormat, "Product code contains unsupported characters.");
        }

        return new ProductCode(normalizedCode);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCharacters();
}
