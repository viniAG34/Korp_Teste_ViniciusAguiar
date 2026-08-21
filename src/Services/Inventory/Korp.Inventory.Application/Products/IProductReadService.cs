namespace Korp.Inventory.Application.Products;

public interface IProductReadService
{
    Task<ProductDetails?> GetByIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<ProductSnapshot?> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken);
    Task<ProductPage> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
}
