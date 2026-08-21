namespace Korp.Inventory.Api.Features.Products.Contracts;

public sealed record ProductPageResponse(
    IReadOnlyList<ProductResponse> Items,
    int PageNumber,
    int PageSize,
    long TotalCount,
    int TotalPages);
