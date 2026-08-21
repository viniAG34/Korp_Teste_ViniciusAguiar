using System.Data;
using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Stock;
using Korp.Inventory.Domain.Products;
using Korp.Inventory.Domain.StockMovements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Korp.Inventory.Infrastructure.Persistence;

public sealed class InventoryUnitOfWork(
    InventoryDbContext context,
    IDbContextTransaction transaction) : IInventoryUnitOfWork
{
    public async Task<IReadOnlyDictionary<Guid, Product>> LoadProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.Products
                .Where(product => productIds.Contains(product.Id))
                .OrderBy(product => product.Id)
                .ToDictionaryAsync(product => product.Id, cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        {
            throw new InventoryServiceUnavailableException(exception);
        }
    }

    public async Task<IReadOnlyList<ExistingDeduction>> GetInvoiceDeductionsAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.StockMovements.AsNoTracking()
                .Where(movement => movement.InvoiceId == invoiceId)
                .OrderBy(movement => movement.ProductId)
                .Select(movement => new ExistingDeduction(movement.ProductId, movement.Quantity))
                .ToArrayAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        {
            throw new InventoryServiceUnavailableException(exception);
        }
    }

    public void AddMovement(StockMovement movement) => context.StockMovements.Add(movement);

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InventoryConcurrencyException(exception);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        {
            throw new InventoryServiceUnavailableException(exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }
}

public sealed class InventoryUnitOfWorkFactory(
    IDbContextFactory<InventoryDbContext> contextFactory) : IInventoryUnitOfWorkFactory
{
    public async Task<IInventoryUnitOfWork> CreateAsync(CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            return new InventoryUnitOfWork(context, transaction);
        }
        catch (Exception exception)
        {
            await context.DisposeAsync();
            if (DatabaseErrorClassifier.IsUnavailable(exception))
            {
                throw new InventoryServiceUnavailableException(exception);
            }

            throw;
        }
    }
}
