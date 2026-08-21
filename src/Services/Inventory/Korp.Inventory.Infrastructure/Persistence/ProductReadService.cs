using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Products;
using Microsoft.EntityFrameworkCore;

namespace Korp.Inventory.Infrastructure.Persistence;

public sealed class ProductReadService(InventoryDbContext context) : IProductReadService
{
    public async Task<ProductDetails?> GetByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        var row = await ExecuteAsync(() => context.Products.AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => new ProductReadRow(
                product.Id,
                product.Code,
                product.Description,
                product.Balance,
                product.CreatedAtUtc,
                product.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<ProductSnapshot?> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken)
    {
        var row = await ExecuteAsync(() => context.Products.AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => new ProductSnapshotRow(product.Id, product.Code, product.Description))
            .SingleOrDefaultAsync(cancellationToken));
        return row is null ? null : new ProductSnapshot(row.Id, row.Code.Value, row.Description);
    }

    public async Task<ProductPage> ListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = context.Products.AsNoTracking();
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(product => product.Code)
                .ThenBy(product => product.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(product => new ProductReadRow(
                    product.Id,
                    product.Code,
                    product.Description,
                    product.Balance,
                    product.CreatedAtUtc,
                    product.UpdatedAtUtc))
                .ToArrayAsync(cancellationToken);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
            return new ProductPage(items.Select(Map).ToArray(), pageNumber, pageSize, totalCount, totalPages);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        {
            throw new InventoryServiceUnavailableException(exception);
        }
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        {
            throw new InventoryServiceUnavailableException(exception);
        }
    }

    private static ProductDetails Map(ProductReadRow row) => new(
        row.Id, row.Code.Value, row.Description, row.Balance, row.CreatedAtUtc, row.UpdatedAtUtc);

    private sealed record ProductReadRow(
        Guid Id,
        Korp.Inventory.Domain.Products.ProductCode Code,
        string Description,
        int Balance,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProductSnapshotRow(
        Guid Id,
        Korp.Inventory.Domain.Products.ProductCode Code,
        string Description);
}
