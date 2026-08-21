using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Stock;
using Korp.Inventory.Domain.Products;
using Korp.Inventory.Domain.StockMovements;

namespace Korp.Inventory.UnitTests.Stock;

public sealed class DeductInvoiceStockHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidMultipleProductsAreDeductedAndCommittedTogether()
    {
        var first = Product.Create(Guid.NewGuid(), "P-1", "First", 5, Guid.NewGuid(), Now);
        var second = Product.Create(Guid.NewGuid(), "P-2", "Second", 4, Guid.NewGuid(), Now);
        var unit = new FakeUnit([first, second]);
        var handler = CreateHandler(new FakeFactory(unit));

        var result = await handler.HandleAsync(Command(
            new DeductInvoiceStockItem(first.Id, 2),
            new DeductInvoiceStockItem(second.Id, 3)), TestContext.Current.CancellationToken);

        Assert.Equal(DeductionStatus.Completed, result.Status);
        Assert.Equal(3, first.Balance);
        Assert.Equal(1, second.Balance);
        Assert.Equal(2, unit.Movements.Count);
        Assert.True(unit.Committed);
    }

    [Fact]
    public async Task MissingOrInsufficientProductRejectsWithoutMutation()
    {
        var product = Product.Create(Guid.NewGuid(), "P-1", "First", 1, Guid.NewGuid(), Now);
        var missingUnit = new FakeUnit([product]);
        var missing = await CreateHandler(new FakeFactory(missingUnit)).HandleAsync(
            Command(
                new DeductInvoiceStockItem(product.Id, 1),
                new DeductInvoiceStockItem(Guid.NewGuid(), 1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(DeductionReason.ProductNotFound, missing.Reason);
        Assert.Equal(1, product.Balance);
        Assert.Empty(missingUnit.Movements);

        var insufficientUnit = new FakeUnit([product]);
        var insufficient = await CreateHandler(new FakeFactory(insufficientUnit)).HandleAsync(
            Command(new DeductInvoiceStockItem(product.Id, 2)), TestContext.Current.CancellationToken);

        Assert.Equal(DeductionReason.InsufficientStock, insufficient.Reason);
        Assert.Equal(1, insufficient.Failures.Single().AvailableBalance);
        Assert.Equal(1, product.Balance);
    }

    [Fact]
    public async Task EquivalentPreviousDeductionCompletesWithoutNewEffect()
    {
        var productId = Guid.NewGuid();
        var unit = new FakeUnit([], [new ExistingDeduction(productId, 2)]);

        var result = await CreateHandler(new FakeFactory(unit)).HandleAsync(
            Command(new DeductInvoiceStockItem(productId, 2)), TestContext.Current.CancellationToken);

        Assert.Equal(DeductionStatus.Completed, result.Status);
        Assert.Empty(unit.Movements);
        Assert.False(unit.Committed);
    }

    [Fact]
    public async Task DivergentPreviousDeductionIsTechnicalInconsistency()
    {
        var productId = Guid.NewGuid();
        var unit = new FakeUnit([], [new ExistingDeduction(productId, 1)]);

        await Assert.ThrowsAsync<InventoryConsistencyException>(() =>
            CreateHandler(new FakeFactory(unit)).HandleAsync(
                Command(new DeductInvoiceStockItem(productId, 2)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrencyCreatesFreshUnitAndStopsAfterThirdAttempt()
    {
        var productId = Guid.NewGuid();
        var units = Enumerable.Range(0, 3)
            .Select(_ => new FakeUnit(
                [Product.Create(productId, "P-1", "First", 1, Guid.NewGuid(), Now)],
                failCommit: true))
            .ToArray();
        var factory = new FakeFactory(units);

        await Assert.ThrowsAsync<InventoryConsistencyException>(() =>
            CreateHandler(factory).HandleAsync(
                Command(new DeductInvoiceStockItem(productId, 1)), TestContext.Current.CancellationToken));

        Assert.Equal(3, factory.CreatedCount);
    }

    [Fact]
    public async Task InvalidCommandIsRejectedBeforeOpeningUnit()
    {
        var factory = new FakeFactory();
        var command = new DeductInvoiceStockCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), []);

        var result = await CreateHandler(factory).HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(DeductionReason.InvalidRequest, result.Reason);
        Assert.Equal(0, factory.CreatedCount);
    }

    private static DeductInvoiceStockHandler CreateHandler(IInventoryUnitOfWorkFactory factory) =>
        new(factory, new FakeGuidGenerator(), new FixedTimeProvider(), new FakeTelemetry());

    private static DeductInvoiceStockCommand Command(params DeductInvoiceStockItem[] items) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), items);

    private sealed class FakeFactory(params FakeUnit[] units) : IInventoryUnitOfWorkFactory
    {
        private readonly Queue<FakeUnit> _units = new(units);
        public int CreatedCount { get; private set; }

        public Task<IInventoryUnitOfWork> CreateAsync(CancellationToken cancellationToken)
        {
            CreatedCount++;
            return Task.FromResult<IInventoryUnitOfWork>(_units.Dequeue());
        }
    }

    private sealed class FakeUnit(
        IReadOnlyList<Product> products,
        IReadOnlyList<ExistingDeduction>? existing = null,
        bool failCommit = false) : IInventoryUnitOfWork
    {
        public List<StockMovement> Movements { get; } = [];
        public bool Committed { get; private set; }

        public Task<IReadOnlyDictionary<Guid, Product>> LoadProductsAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Product>>(
                products.Where(product => productIds.Contains(product.Id)).ToDictionary(product => product.Id));

        public Task<IReadOnlyList<ExistingDeduction>> GetInvoiceDeductionsAsync(
            Guid invoiceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(existing ?? (IReadOnlyList<ExistingDeduction>)[]);

        public void AddMovement(StockMovement movement) => Movements.Add(movement);

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            if (failCommit) throw new InventoryConcurrencyException(new InvalidOperationException());
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeGuidGenerator : IGuidGenerator
    {
        public Guid NewGuid() => Guid.NewGuid();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeTelemetry : IInventoryTelemetry
    {
        public void RecordStockDeduction(string outcome, TimeSpan duration) { }
        public void RecordConcurrencyConflict() { }
    }
}
