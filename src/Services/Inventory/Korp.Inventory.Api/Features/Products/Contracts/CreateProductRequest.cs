namespace Korp.Inventory.Api.Features.Products.Contracts;

public sealed record CreateProductRequest(string Code, string Description, int InitialBalance);
