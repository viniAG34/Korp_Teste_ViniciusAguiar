namespace Korp.Inventory.Api.Features.Products.Contracts;

public sealed record ProductResponse(
    Guid Id,
    string Code,
    string Description,
    int Balance,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
