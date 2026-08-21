using Korp.Inventory.Domain.Products;

namespace Korp.Inventory.Application.Products;

public interface IProductRepository
{
    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);
    void Add(Product product);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
