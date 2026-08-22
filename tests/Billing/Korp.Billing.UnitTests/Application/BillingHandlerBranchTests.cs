using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;

namespace Korp.Billing.UnitTests.Application;

public sealed class BillingHandlerBranchTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAndQueryHandlersUseTheirSpecificPorts()
    {
        var repository = new RepositoryFake();
        var reads = new ReadServiceFake(repository);
        var created = await new CreateInvoiceHandler(repository, reads, new NumberGeneratorFake(),
            new GuidGeneratorFake(), new FixedTimeProvider(), new TelemetryFake())
            .HandleAsync(new CreateInvoiceCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);
        var detail = await new GetInvoiceByIdHandler(reads)
            .HandleAsync(new GetInvoiceByIdQuery(created.Id), TestContext.Current.CancellationToken);
        var page = await new ListInvoicesHandler(reads)
            .HandleAsync(new ListInvoicesQuery(1, 20), TestContext.Current.CancellationToken);

        Assert.Equal(42, created.Number);
        Assert.Equal(created, detail);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task AddItemCoversNotFoundVersionStateCatalogAndSuccess()
    {
        var catalog = new CatalogFake(new ProductSnapshot(Guid.Parse("10000000-0000-0000-0000-000000000001"), "P-1", "Produto"));
        var handler = CreateAddHandler(new RepositoryFake(), catalog);
        Assert.Equal(InvoiceMutationStatus.InvoiceNotFound, (await handler.HandleAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), 1, 0), TestContext.Current.CancellationToken)).Status);

        var open = CreateInvoice();
        handler = CreateAddHandler(new RepositoryFake(open), catalog);
        Assert.Equal(InvoiceMutationStatus.VersionMismatch, (await handler.HandleAsync(
            new(open.Id, Guid.NewGuid(), 1, 99), TestContext.Current.CancellationToken)).Status);

        var closed = CreateInvoiceWithItem();
        closed.StartIssuance(Now.AddSeconds(1));
        closed.CompleteIssuance(Now.AddSeconds(2));
        handler = CreateAddHandler(new RepositoryFake(closed), catalog);
        Assert.Equal(InvoiceMutationStatus.InvoiceNotOpen, (await handler.HandleAsync(
            new(closed.Id, Guid.NewGuid(), 1, closed.Version), TestContext.Current.CancellationToken)).Status);

        var active = CreateInvoiceWithItem();
        active.StartIssuance(Now.AddSeconds(1));
        handler = CreateAddHandler(new RepositoryFake(active), catalog);
        Assert.Equal(InvoiceMutationStatus.IssuanceInProgress, (await handler.HandleAsync(
            new(active.Id, Guid.NewGuid(), 1, active.Version), TestContext.Current.CancellationToken)).Status);

        open = CreateInvoice();
        handler = CreateAddHandler(new RepositoryFake(open), new CatalogFake(null));
        Assert.Equal(InvoiceMutationStatus.ProductNotFound, (await handler.HandleAsync(
            new(open.Id, Guid.NewGuid(), 1, open.Version), TestContext.Current.CancellationToken)).Status);

        open = CreateInvoice();
        var product = new ProductSnapshot(Guid.NewGuid(), "P-2", "Outro");
        handler = CreateAddHandler(new RepositoryFake(open), new CatalogFake(product));
        var success = await handler.HandleAsync(new(open.Id, product.Id, 2, open.Version), TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceMutationStatus.Success, success.Status);
        Assert.Single(success.Invoice!.Items);
    }

    [Fact]
    public async Task UpdateAndRemoveCoverSuccessMissingItemAndConcurrency()
    {
        var invoice = CreateInvoiceWithItem();
        var itemId = invoice.Items.Single().Id;
        var repository = new RepositoryFake(invoice);
        var update = new UpdateInvoiceItemQuantityHandler(repository, new FixedTimeProvider(), new TelemetryFake());
        var updated = await update.HandleAsync(new(invoice.Id, itemId, 3, invoice.Version), TestContext.Current.CancellationToken);
        Assert.Equal(3, updated.Invoice!.Items.Single().Quantity);

        var missing = await update.HandleAsync(new(invoice.Id, Guid.NewGuid(), 2, invoice.Version), TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceMutationStatus.ItemNotFound, missing.Status);

        invoice = CreateInvoiceWithItem();
        repository = new RepositoryFake(invoice) { ThrowConcurrency = true };
        var remove = new RemoveInvoiceItemHandler(repository, new FixedTimeProvider(), new TelemetryFake());
        var conflict = await remove.HandleAsync(new(invoice.Id, invoice.Items.Single().Id, invoice.Version), TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceMutationStatus.VersionMismatch, conflict.Status);

        invoice = CreateInvoiceWithItem();
        repository = new RepositoryFake(invoice);
        remove = new RemoveInvoiceItemHandler(repository, new FixedTimeProvider(), new TelemetryFake());
        var removed = await remove.HandleAsync(new(invoice.Id, invoice.Items.Single().Id, invoice.Version), TestContext.Current.CancellationToken);
        Assert.Empty(removed.Invoice!.Items);
    }

    [Fact]
    public async Task PrintCoversEligibilityFailuresAndCreatesDurableRequest()
    {
        Assert.Equal(PrintInvoiceStatus.InvoiceNotFound, (await Print(null, 0)).Status);
        var empty = CreateInvoice();
        Assert.Equal(PrintInvoiceStatus.VersionMismatch, (await Print(empty, 99)).Status);
        Assert.Equal(PrintInvoiceStatus.InvoiceEmpty, (await Print(empty, empty.Version)).Status);

        var active = CreateInvoiceWithItem(); active.StartIssuance(Now.AddSeconds(1));
        Assert.Equal(PrintInvoiceStatus.IssuanceInProgress, (await Print(active, active.Version)).Status);

        var closed = CreateInvoiceWithItem(); closed.StartIssuance(Now.AddSeconds(1)); closed.CompleteIssuance(Now.AddSeconds(2));
        Assert.Equal(PrintInvoiceStatus.InvoiceNotOpen, (await Print(closed, closed.Version)).Status);

        var valid = CreateInvoiceWithItem();
        var unit = new UnitFake(valid, null);
        var read = new IssuanceReadFake();
        var handler = new PrintInvoiceHandler(new UnitFactoryFake(unit), read,
            new GuidGeneratorFake(), new FixedTimeProvider(), new TelemetryFake());
        read.AfterCommit = () => unit.Process is null ? null : Persist(unit.Process, valid.Version);
        var accepted = await handler.HandleAsync(new(valid.Id, Guid.NewGuid(), valid.Version, Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        Assert.Equal(PrintInvoiceStatus.Accepted, accepted.Status);
        Assert.NotNull(unit.Outbox);
        Assert.True(valid.IsIssuanceInProgress);
    }

    [Fact]
    public async Task IdempotencyKeyFromAnotherInvoiceIsRejectedWithoutUnit()
    {
        var known = new PersistedIssuanceProcess(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InvoiceIssuanceProcessStatus.Pending, Now, Now, null, null, null, 1);
        var read = new IssuanceReadFake { Known = known };
        var factory = new UnitFactoryFake(null);
        var result = await new PrintInvoiceHandler(factory, read, new GuidGeneratorFake(), new FixedTimeProvider(), new TelemetryFake())
            .HandleAsync(new(Guid.NewGuid(), known.IdempotencyKey, 0, Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(PrintInvoiceStatus.IdempotencyKeyReused, result.Status);
        Assert.Equal(0, factory.CreateCount);
    }

    [Theory]
    [InlineData(IssuanceTransitionKind.Completed, InvoiceIssuanceProcessStatus.Completed, InvoiceStatus.Closed, false)]
    [InlineData(IssuanceTransitionKind.Rejected, InvoiceIssuanceProcessStatus.Rejected, InvoiceStatus.Open, false)]
    [InlineData(IssuanceTransitionKind.ManualIntervention, InvoiceIssuanceProcessStatus.ManualIntervention, InvoiceStatus.Open, true)]
    public async Task TransitionHandlerCoordinatesInvoiceAndProcess(
        IssuanceTransitionKind kind, InvoiceIssuanceProcessStatus expectedProcess, InvoiceStatus expectedInvoice, bool blocked)
    {
        var invoice = CreateInvoiceWithItem(); invoice.StartIssuance(Now.AddSeconds(1));
        var process = InvoiceIssuanceProcess.Create(Guid.NewGuid(), invoice.Id, Guid.NewGuid(), Guid.NewGuid(), Now.AddSeconds(1));
        var unit = new UnitFake(invoice, process);
        await new TransitionInvoiceIssuanceHandler(new UnitFactoryFake(unit), new FixedTimeProvider(), new TelemetryFake())
            .HandleAsync(new(process.Id, invoice.Id, kind,
                kind is IssuanceTransitionKind.Rejected or IssuanceTransitionKind.ManualIntervention ? "known_code" : null,
                kind is IssuanceTransitionKind.Rejected or IssuanceTransitionKind.ManualIntervention ? "Descrição" : null),
                TestContext.Current.CancellationToken);
        Assert.Equal(expectedProcess, process.Status);
        Assert.Equal(expectedInvoice, invoice.Status);
        Assert.Equal(blocked, invoice.IsIssuanceInProgress);
        Assert.Equal(1, unit.CommitCount);
    }

    private static AddInvoiceItemHandler CreateAddHandler(RepositoryFake repository, IProductCatalogClient catalog) =>
        new(repository, catalog, new GuidGeneratorFake(), new FixedTimeProvider(), new TelemetryFake());

    private static async Task<PrintInvoiceResult> Print(Invoice? invoice, uint version)
    {
        var unit = new UnitFake(invoice, null);
        return await new PrintInvoiceHandler(new UnitFactoryFake(unit), new IssuanceReadFake(),
            new GuidGeneratorFake(), new FixedTimeProvider(), new TelemetryFake())
            .HandleAsync(new(invoice?.Id ?? Guid.NewGuid(), Guid.NewGuid(), version, Guid.NewGuid(), Guid.NewGuid()),
                TestContext.Current.CancellationToken);
    }

    private static Invoice CreateInvoice() => Invoice.Create(Guid.NewGuid(), 42, Guid.NewGuid(), Now);
    private static Invoice CreateInvoiceWithItem()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P-1", "Produto", 1, Now);
        return invoice;
    }

    private static PersistedIssuanceProcess Persist(InvoiceIssuanceProcess process, uint version) => new(
        process.Id, process.InvoiceId, process.IdempotencyKey, process.Status, process.CreatedAtUtc,
        process.UpdatedAtUtc, process.FinishedAtUtc, process.OutcomeCode, process.OutcomeDescription, version);

    private sealed class RepositoryFake(Invoice? invoice = null) : IInvoiceRepository
    {
        public bool ThrowConcurrency { get; init; }
        public Invoice? Invoice { get; private set; } = invoice;
        public Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken) => Task.FromResult(Invoice);
        public void Add(Invoice value) => Invoice = value;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => ThrowConcurrency
            ? Task.FromException(new BillingConcurrencyException()) : Task.CompletedTask;
    }

    private sealed class ReadServiceFake(RepositoryFake repository) : IInvoiceReadService
    {
        public Task<InvoiceDetails?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken) =>
            Task.FromResult(repository.Invoice?.ToDetails());
        public Task<InvoicePage> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new InvoicePage(repository.Invoice is null ? [] : [new InvoiceSummary(repository.Invoice.Id, repository.Invoice.Number,
                repository.Invoice.Status, repository.Invoice.IsIssuanceInProgress, repository.Invoice.Items.Count,
                repository.Invoice.CreatedAtUtc, repository.Invoice.UpdatedAtUtc)], pageNumber, pageSize,
                repository.Invoice is null ? 0 : 1, repository.Invoice is null ? 0 : 1));
    }

    private sealed class NumberGeneratorFake : IInvoiceNumberGenerator { public Task<long> GetNextAsync(CancellationToken cancellationToken) => Task.FromResult(42L); }
    private sealed class CatalogFake(ProductSnapshot? snapshot) : IProductCatalogClient { public Task<ProductSnapshot?> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken) => Task.FromResult(snapshot); }
    private sealed class GuidGeneratorFake : IGuidGenerator { public Guid NewGuid() => Guid.NewGuid(); }
    private sealed class FixedTimeProvider : TimeProvider { public override DateTimeOffset GetUtcNow() => Now.AddSeconds(3); }
    private sealed class TelemetryFake : IBillingTelemetry
    { public void InvoiceCreated() { } public void ItemOperation(string operation, string outcome) { } public void IssuanceRequested(string outcome) { } public void IssuanceTransitioned(string status) { } public void ProductCatalogRequest(string outcome) { } }

    private sealed class UnitFactoryFake(IBillingUnitOfWork? unit) : IBillingUnitOfWorkFactory
    { public int CreateCount { get; private set; } public Task<IBillingUnitOfWork> CreateAsync(CancellationToken cancellationToken) { CreateCount++; return Task.FromResult(unit!); } }

    private sealed class UnitFake(Invoice? invoice, InvoiceIssuanceProcess? process) : IBillingUnitOfWork
    {
        public InvoiceIssuanceProcess? Process { get; private set; } = process;
        public StockDeductionOutboxRequest? Outbox { get; private set; }
        public int CommitCount { get; private set; }
        public Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) => Task.FromResult(invoice);
        public Task<InvoiceIssuanceProcess?> GetProcessByIdAsync(Guid processId, CancellationToken cancellationToken) => Task.FromResult(Process);
        public Task<InvoiceIssuanceProcess?> GetProcessByKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(Process);
        public void AddProcess(InvoiceIssuanceProcess value) => Process = value;
        public void AddOutbox(StockDeductionOutboxRequest request) => Outbox = request;
        public Task CommitAsync(CancellationToken cancellationToken) { CommitCount++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class IssuanceReadFake : IIssuanceProcessReadService
    {
        public PersistedIssuanceProcess? Known { get; init; }
        public Func<PersistedIssuanceProcess?>? AfterCommit { get; set; }
        public Task<PersistedIssuanceProcess?> GetByIdAsync(Guid processId, CancellationToken cancellationToken) => Task.FromResult(AfterCommit?.Invoke() ?? Known);
        public Task<PersistedIssuanceProcess?> GetByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(Known?.IdempotencyKey == idempotencyKey ? Known : null);
        public Task<PersistedIssuanceProcess?> GetActiveByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken) =>
            Task.FromResult(Known?.InvoiceId == invoiceId && Known.Status is InvoiceIssuanceProcessStatus.Pending or InvoiceIssuanceProcessStatus.AwaitingStock ? Known : null);
    }
}
