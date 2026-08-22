using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Stock;
using Korp.Inventory.Domain.Products;
using Korp.Inventory.Domain.StockMovements;

namespace Korp.Inventory.UnitTests.Stock;

public sealed class FinalizeStockDeductionFailureHandlerTests
{
    private static readonly DeductInvoiceStockCommand Command = new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), [new(Guid.NewGuid(), 1)], Guid.NewGuid(), new string('A', 64));

    [Fact]
    public async Task AbsenceOfEffectsPersistsInboxAndProcessingFailedTogether()
    {
        var unit = new Unit();
        var handler = new FinalizeStockDeductionFailureHandler(new Factory(unit), new Guids(), TimeProvider.System);
        var result = await handler.HandleAsync(Command, TestContext.Current.CancellationToken);
        Assert.Equal(TerminalFailureStatus.Confirmed, result);
        Assert.True(unit.Committed);
        Assert.NotNull(unit.Failure);
        Assert.Equal(Command.EventId, unit.Failure!.CausationId);
    }

    [Fact]
    public async Task ExistingMovementMakesOutcomeInconclusive()
    {
        var unit = new Unit([new ExistingDeduction(Command.Items[0].ProductId, 1)]);
        var handler = new FinalizeStockDeductionFailureHandler(new Factory(unit), new Guids(), TimeProvider.System);
        Assert.Equal(TerminalFailureStatus.Inconclusive,
            await handler.HandleAsync(Command, TestContext.Current.CancellationToken));
        Assert.False(unit.Committed);
    }

    private sealed class Factory(Unit unit) : IInventoryUnitOfWorkFactory
    { public Task<IInventoryUnitOfWork> CreateAsync(CancellationToken token) => Task.FromResult<IInventoryUnitOfWork>(unit); }
    private sealed class Guids : IGuidGenerator { public Guid NewGuid() => Guid.NewGuid(); }
    private sealed class Unit(IReadOnlyList<ExistingDeduction>? movements = null) : IInventoryUnitOfWork
    {
        public bool Committed { get; private set; }
        public StockDeductionProcessingFailedRequest? Failure { get; private set; }
        public Task<ProcessedMessage?> GetProcessedMessageAsync(Guid id, CancellationToken token) => Task.FromResult<ProcessedMessage?>(null);
        public Task<IReadOnlyList<ExistingDeduction>> GetInvoiceDeductionsAsync(Guid id, CancellationToken token) =>
            Task.FromResult(movements ?? (IReadOnlyList<ExistingDeduction>)[]);
        public Task<IReadOnlyDictionary<Guid, Product>> LoadProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => throw new NotSupportedException();
        public void AddProcessedMessage(ProcessedMessageRequest request) { }
        public void AddProcessingFailedOutbox(StockDeductionProcessingFailedRequest request) => Failure = request;
        public void AddMovement(StockMovement movement) => throw new NotSupportedException();
        public void AddResultOutbox(StockDeductionResultRequest request) => throw new NotSupportedException();
        public Task CommitAsync(CancellationToken token) { Committed = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
