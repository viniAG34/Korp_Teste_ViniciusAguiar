using System.Data;
using System.Text.Json;
using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Stock;
using Korp.Inventory.Domain.Products;
using Korp.Inventory.Domain.StockMovements;
using Korp.Inventory.Infrastructure.Persistence.Messaging;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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

    public async Task<ProcessedMessage?> GetProcessedMessageAsync(Guid messageId, CancellationToken cancellationToken) =>
        await context.InboxMessages.AsNoTracking()
            .Where(message => message.MessageId == messageId)
            .Select(message => new ProcessedMessage(message.PayloadHash))
            .SingleOrDefaultAsync(cancellationToken);

    public void AddProcessedMessage(ProcessedMessageRequest request) =>
        context.InboxMessages.Add(InboxMessage.Create(request.MessageId,
            IntegrationEventTypes.StockDeductionRequested, 1, request.CorrelationId, null,
            request.PayloadHash, request.ProcessedAtUtc));

    public void AddResultOutbox(StockDeductionResultRequest request)
    {
        var (messageType, payload) = request.Result.Status switch
        {
            DeductionStatus.Completed => (IntegrationEventTypes.StockDeductionCompleted, (object)
                new StockDeductionCompletedV1(request.IssuanceProcessId, request.InvoiceId)),
            DeductionStatus.Rejected => (IntegrationEventTypes.StockDeductionRejected, (object)
                new StockDeductionRejectedV1(request.IssuanceProcessId, request.InvoiceId,
                    ReasonCode(request.Result.Reason), ReasonDescription(request.Result.Reason),
                    request.Result.Failures.Select(failure => new StockDeductionFailureV1(
                        failure.ProductId, failure.RequestedQuantity, failure.AvailableBalance)).ToArray())),
            _ => throw new InvalidOperationException("Duplicate deductions do not produce another result.")
        };
        var envelope = new IntegrationEventEnvelope<object>(request.MessageId, messageType, 1,
            request.OccurredAtUtc, request.CorrelationId, request.CausationId,
            IntegrationEventProducers.Inventory, payload);
        context.OutboxMessages.Add(OutboxMessage.Create(request.MessageId, messageType, 1,
            JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web), request.CorrelationId,
            request.CausationId, request.OccurredAtUtc));
    }

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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_inbox_messages" })
        {
            throw new InventoryConcurrencyException(exception);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        {
            throw new InventoryServiceUnavailableException(exception);
        }
    }

    private static string ReasonCode(DeductionReason? reason) => reason switch
    {
        DeductionReason.InvalidRequest => StockDeductionReasonCodes.InvalidRequest,
        DeductionReason.ProductNotFound => StockDeductionReasonCodes.ProductNotFound,
        DeductionReason.InsufficientStock => StockDeductionReasonCodes.InsufficientStock,
        _ => throw new InvalidOperationException("Rejection reason is required.")
    };

    private static string ReasonDescription(DeductionReason? reason) => reason switch
    {
        DeductionReason.InvalidRequest => "The stock deduction request is invalid.",
        DeductionReason.ProductNotFound => "One or more products were not found.",
        DeductionReason.InsufficientStock => "One or more products have insufficient stock.",
        _ => throw new InvalidOperationException("Rejection reason is required.")
    };

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
