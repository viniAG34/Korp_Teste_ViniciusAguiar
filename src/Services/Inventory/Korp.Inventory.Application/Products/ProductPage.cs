namespace Korp.Inventory.Application.Products;

public sealed record ProductPage(
    IReadOnlyList<ProductDetails> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
