using Korp.Inventory.Application.Common;
using Korp.Inventory.Domain.Products;
using Korp.Inventory.Domain.StockMovements;

namespace Korp.Inventory.Application.Stock;

public sealed record DeductInvoiceStockItem(Guid ProductId, int Quantity);

public sealed record DeductInvoiceStockCommand(
    Guid EventId,
    Guid IssuanceProcessId,
    Guid InvoiceId,
    IReadOnlyList<DeductInvoiceStockItem> Items,
    Guid CorrelationId,
    string PayloadHash);

public enum DeductionStatus { Completed, Rejected, Duplicate }
public enum DeductionReason { InvalidRequest, ProductNotFound, InsufficientStock }

public sealed record DeductionFailure(Guid ProductId, int RequestedQuantity, int? AvailableBalance = null);

public sealed record DeductionResult(
    DeductionStatus Status,
    DeductionReason? Reason,
    IReadOnlyList<DeductionFailure> Failures,
    int AttemptCount)
{
    public static DeductionResult Completed(int attemptCount) =>
        new(DeductionStatus.Completed, null, [], attemptCount);

    public static DeductionResult Rejected(
        DeductionReason reason,
        IReadOnlyList<DeductionFailure> failures,
        int attemptCount = 0) =>
        new(DeductionStatus.Rejected, reason, failures, attemptCount);
}

public sealed record ExistingDeduction(Guid ProductId, int Quantity);
public sealed record ProcessedMessage(string PayloadHash);
public sealed record ProcessedMessageRequest(Guid MessageId, Guid CorrelationId, string PayloadHash, DateTimeOffset ProcessedAtUtc);
public sealed record StockDeductionResultRequest(Guid MessageId, Guid CorrelationId, Guid CausationId,
    Guid IssuanceProcessId, Guid InvoiceId, DeductionResult Result, DateTimeOffset OccurredAtUtc);

public interface IInventoryUnitOfWork : IAsyncDisposable
{
    Task<IReadOnlyDictionary<Guid, Product>> LoadProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExistingDeduction>> GetInvoiceDeductionsAsync(
        Guid invoiceId,
        CancellationToken cancellationToken);

    Task<ProcessedMessage?> GetProcessedMessageAsync(Guid messageId, CancellationToken cancellationToken);
    void AddMovement(StockMovement movement);
    void AddProcessedMessage(ProcessedMessageRequest request);
    void AddResultOutbox(StockDeductionResultRequest request);
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IInventoryUnitOfWorkFactory
{
    Task<IInventoryUnitOfWork> CreateAsync(CancellationToken cancellationToken);
}

public sealed class InventoryConcurrencyException(Exception innerException)
    : Exception("Inventory state changed concurrently.", innerException);

public sealed class InventoryMessageIntegrityException()
    : Exception("A processed message identifier was reused with different content.");

public sealed class DeductInvoiceStockHandler(
    IInventoryUnitOfWorkFactory unitOfWorkFactory,
    IGuidGenerator guidGenerator,
    TimeProvider timeProvider,
    IInventoryTelemetry telemetry)
{
    private const int MaximumAttempts = 3;

    public async Task<DeductionResult> HandleAsync(
        DeductInvoiceStockCommand command,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        var requested = (command.Items ?? []).OrderBy(item => item.ProductId).ToArray();
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var unit = await unitOfWorkFactory.CreateAsync(cancellationToken);
            try
            {
                var processed = await unit.GetProcessedMessageAsync(command.EventId, cancellationToken);
                if (processed is not null)
                {
                    if (!string.Equals(processed.PayloadHash, command.PayloadHash, StringComparison.Ordinal))
                        throw new InventoryMessageIntegrityException();
                    telemetry.RecordStockDeduction("duplicate", timeProvider.GetElapsedTime(startedAt));
                    return new DeductionResult(DeductionStatus.Duplicate, null, [], attempt);
                }

                var invalid = Validate(command);
                if (invalid.Count > 0)
                {
                    var rejected = DeductionResult.Rejected(DeductionReason.InvalidRequest, invalid, attempt);
                    await CommitResultAsync(unit, command, rejected, cancellationToken);
                    telemetry.RecordStockDeduction("rejected", timeProvider.GetElapsedTime(startedAt));
                    return rejected;
                }

                var existing = await unit.GetInvoiceDeductionsAsync(command.InvoiceId, cancellationToken);
                if (existing.Count > 0)
                {
                    if (Equivalent(requested, existing))
                    {
                        var completed = DeductionResult.Completed(attempt);
                        await CommitResultAsync(unit, command, completed, cancellationToken);
                        telemetry.RecordStockDeduction("completed", timeProvider.GetElapsedTime(startedAt));
                        return completed;
                    }

                    throw new InventoryLogicalDivergenceException(
                        "Existing invoice deductions differ from the requested content.");
                }

                var ids = requested.Select(item => item.ProductId).ToArray();
                var products = await unit.LoadProductsAsync(ids, cancellationToken);
                var missing = requested
                    .Where(item => !products.ContainsKey(item.ProductId))
                    .Select(item => new DeductionFailure(item.ProductId, item.Quantity))
                    .ToArray();
                if (missing.Length > 0)
                {
                    var rejected = DeductionResult.Rejected(DeductionReason.ProductNotFound, missing, attempt);
                    await CommitResultAsync(unit, command, rejected, cancellationToken);
                    telemetry.RecordStockDeduction("rejected", timeProvider.GetElapsedTime(startedAt));
                    return rejected;
                }

                var insufficient = requested
                    .Where(item => products[item.ProductId].Balance < item.Quantity)
                    .Select(item => new DeductionFailure(
                        item.ProductId,
                        item.Quantity,
                        products[item.ProductId].Balance))
                    .ToArray();
                if (insufficient.Length > 0)
                {
                    var rejected = DeductionResult.Rejected(DeductionReason.InsufficientStock, insufficient, attempt);
                    await CommitResultAsync(unit, command, rejected, cancellationToken);
                    telemetry.RecordStockDeduction("rejected", timeProvider.GetElapsedTime(startedAt));
                    return rejected;
                }

                var occurredAtUtc = timeProvider.GetUtcNow();
                foreach (var item in requested)
                {
                    var movement = products[item.ProductId].DeductForInvoice(
                        guidGenerator.NewGuid(),
                        command.InvoiceId,
                        command.EventId,
                        item.Quantity,
                        occurredAtUtc);
                    unit.AddMovement(movement);
                }

                var completedResult = DeductionResult.Completed(attempt);
                await CommitResultAsync(unit, command, completedResult, cancellationToken);
                telemetry.RecordStockDeduction("completed", timeProvider.GetElapsedTime(startedAt));
                return completedResult;
            }
            catch (InventoryConcurrencyException)
            {
                telemetry.RecordConcurrencyConflict();
                if (attempt == MaximumAttempts)
                {
                    break;
                }

                // A fresh unit is created by the next iteration.
            }
        }

        telemetry.RecordStockDeduction("technical_failure", timeProvider.GetElapsedTime(startedAt));
        throw new InventoryConsistencyException(
            $"Inventory concurrency could not be resolved after {MaximumAttempts} attempts.");
    }

    private async Task CommitResultAsync(IInventoryUnitOfWork unit, DeductInvoiceStockCommand command,
        DeductionResult result, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        unit.AddProcessedMessage(new ProcessedMessageRequest(command.EventId, command.CorrelationId,
            command.PayloadHash, now));
        unit.AddResultOutbox(new StockDeductionResultRequest(guidGenerator.NewGuid(), command.CorrelationId,
            command.EventId, command.IssuanceProcessId, command.InvoiceId, result, now));
        await unit.CommitAsync(cancellationToken);
    }

    private static List<DeductionFailure> Validate(DeductInvoiceStockCommand command)
    {
        if (command.EventId == Guid.Empty
            || command.IssuanceProcessId == Guid.Empty
            || command.InvoiceId == Guid.Empty
            || command.CorrelationId == Guid.Empty
            || command.PayloadHash?.Length != 64
            || command.Items is null
            || command.Items.Count == 0)
        {
            return [new DeductionFailure(Guid.Empty, 0)];
        }

        var invalid = command.Items
            .Where(item => item.ProductId == Guid.Empty || item.Quantity <= 0)
            .Select(item => new DeductionFailure(item.ProductId, item.Quantity))
            .ToList();
        invalid.AddRange(command.Items
            .GroupBy(item => item.ProductId)
            .Where(group => group.Count() > 1)
            .Select(group => new DeductionFailure(group.Key, group.First().Quantity)));
        return invalid;
    }

    private static bool Equivalent(
        DeductInvoiceStockItem[] requested,
        IReadOnlyList<ExistingDeduction> existing) =>
        requested.Length == existing.Count
        && requested.Zip(existing.OrderBy(item => item.ProductId))
            .All(pair => pair.First.ProductId == pair.Second.ProductId
                && pair.First.Quantity == pair.Second.Quantity);
}
