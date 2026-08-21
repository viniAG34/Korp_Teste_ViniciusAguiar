using Korp.Inventory.Application.Products;
using Korp.Inventory.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korp.Inventory.Infrastructure.Persistence;

public sealed class ProductRepository(InventoryDbContext context) : IProductRepository
{
    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.Products.AnyAsync(product => product.Code == ProductCode.Create(normalizedCode), cancellationToken);

    public void Add(Product product) => context.Products.Add(product);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsProductCodeConflict(exception))
        {
            throw new ProductCodeAlreadyExistsException(exception);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        {
            throw new Application.Common.InventoryServiceUnavailableException(exception);
        }
    }

    private static bool IsProductCodeConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_products_code"
        };
}
