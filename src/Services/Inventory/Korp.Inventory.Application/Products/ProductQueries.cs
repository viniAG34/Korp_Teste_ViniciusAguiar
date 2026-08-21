namespace Korp.Inventory.Application.Products;

public sealed record GetProductByIdQuery(Guid ProductId);
public sealed record GetProductSnapshotQuery(Guid ProductId);
public sealed record ListProductsQuery(int PageNumber, int PageSize);

public sealed class GetProductByIdHandler(IProductReadService readService)
{
    public Task<ProductDetails?> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken) =>
        readService.GetByIdAsync(query.ProductId, cancellationToken);
}

public sealed class GetProductSnapshotHandler(IProductReadService readService)
{
    public Task<ProductSnapshot?> HandleAsync(GetProductSnapshotQuery query, CancellationToken cancellationToken) =>
        readService.GetSnapshotAsync(query.ProductId, cancellationToken);
}

public sealed class ListProductsHandler(IProductReadService readService)
{
    public Task<ProductPage> HandleAsync(ListProductsQuery query, CancellationToken cancellationToken) =>
        readService.ListAsync(query.PageNumber, query.PageSize, cancellationToken);
}
