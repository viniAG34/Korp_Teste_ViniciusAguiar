namespace Korp.Inventory.Application.Products;

public sealed record ProductDetails(
    Guid Id,
    string Code,
    string Description,
    int Balance,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
