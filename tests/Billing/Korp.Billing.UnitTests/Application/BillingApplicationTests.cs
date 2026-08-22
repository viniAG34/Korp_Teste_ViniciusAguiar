using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;

namespace Korp.Billing.UnitTests.Application;

public sealed class BillingApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DuplicateProductIsRejectedBeforeCatalogCall()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), 1, Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        invoice.AddItem(Guid.NewGuid(), productId, "PROD-1", "Produto", 1, Now);
        var repository = new InvoiceRepositoryFake(invoice);
        var catalog = new ProductCatalogFake();
        var handler = new AddInvoiceItemHandler(repository, catalog, new GuidGeneratorFake(),
            new FixedTimeProvider(Now), new TelemetryFake());

        var result = await handler.HandleAsync(
            new AddInvoiceItemCommand(invoice.Id, productId, 2, invoice.Version), TestContext.Current.CancellationToken);

        Assert.Equal(InvoiceMutationStatus.ProductAlreadyAdded, result.Status);
        Assert.Equal(0, catalog.CallCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task KnownIdempotencyKeyReplaysBeforeLoadingCurrentInvoice()
    {
        var invoiceId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var persisted = new PersistedIssuanceProcess(
            Guid.NewGuid(), invoiceId, key, InvoiceIssuanceProcessStatus.Completed,
            Now, Now.AddSeconds(1), Now.AddSeconds(1), null, null, 9);
        var read = new IssuanceReadServiceFake(persisted);
        var factory = new UnitFactoryFake(null);
        var handler = new PrintInvoiceHandler(factory, read, new GuidGeneratorFake(),
            new FixedTimeProvider(Now.AddMinutes(1)), new TelemetryFake());

        var result = await handler.HandleAsync(new PrintInvoiceCommand(
            invoiceId, key, 1, Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(PrintInvoiceStatus.ReplayedTerminal, result.Status);
        Assert.Equal((uint)9, result.Process?.InvoiceVersion);
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public void ProcessDelayAndRetryAfterAreDerivedFromControlledTime()
    {
        var process = new PersistedIssuanceProcess(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), InvoiceIssuanceProcessStatus.AwaitingStock,
            Now, Now.AddSeconds(2), null, null, null, 4);

        var details = process.ToDetails(Now.AddSeconds(12));

        Assert.True(details.IsDelayed);
        Assert.Equal(3, details.RetryAfterSeconds);
    }

    [Fact]
    public async Task PublisherConfirmationDoesNotRegressCompletedProcess()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), 1, Guid.NewGuid(), Now);
        invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "PROD-1", "Produto", 1, Now);
        invoice.StartIssuance(Now);
        var process = InvoiceIssuanceProcess.Create(Guid.NewGuid(), invoice.Id, Guid.NewGuid(), Guid.NewGuid(), Now);
        invoice.CompleteIssuance(Now.AddSeconds(1));
        process.Complete(Now.AddSeconds(1));
        var unit = new UnitFake(invoice, process);
        var handler = new TransitionInvoiceIssuanceHandler(new UnitFactoryFake(unit),
            new FixedTimeProvider(Now.AddSeconds(2)), new TelemetryFake());

        await handler.HandleAsync(new TransitionInvoiceIssuanceCommand(
            process.Id, invoice.Id, IssuanceTransitionKind.AwaitingStock), TestContext.Current.CancellationToken);

        Assert.Equal(InvoiceIssuanceProcessStatus.Completed, process.Status);
        Assert.Equal(0, unit.CommitCount);
    }

    private sealed class InvoiceRepositoryFake(Invoice invoice) : IInvoiceRepository
    {
        public int SaveCount { get; private set; }
        public Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken) => Task.FromResult<Invoice?>(invoice);
        public void Add(Invoice value) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class ProductCatalogFake : IProductCatalogClient
    {
        public int CallCount { get; private set; }
        public Task<ProductSnapshot?> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken)
        { CallCount++; return Task.FromResult<ProductSnapshot?>(null); }
    }

    private sealed class IssuanceReadServiceFake(PersistedIssuanceProcess? process) : IIssuanceProcessReadService
    {
        public Task<PersistedIssuanceProcess?> GetByIdAsync(Guid processId, CancellationToken cancellationToken) => Task.FromResult(process);
        public Task<PersistedIssuanceProcess?> GetByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(process?.IdempotencyKey == idempotencyKey ? process : null);
        public Task<PersistedIssuanceProcess?> GetActiveByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken) =>
            Task.FromResult(process?.InvoiceId == invoiceId && process.Status is InvoiceIssuanceProcessStatus.Pending or InvoiceIssuanceProcessStatus.AwaitingStock ? process : null);
    }

    private sealed class UnitFactoryFake(IBillingUnitOfWork? unit) : IBillingUnitOfWorkFactory
    {
        public int CreateCount { get; private set; }
        public Task<IBillingUnitOfWork> CreateAsync(CancellationToken cancellationToken)
        { CreateCount++; return Task.FromResult(unit ?? throw new InvalidOperationException("Unit must not be created.")); }
    }

    private sealed class UnitFake(Invoice invoice, InvoiceIssuanceProcess process) : IBillingUnitOfWork
    {
        public int CommitCount { get; private set; }
        public Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) => Task.FromResult<Invoice?>(invoice);
        public Task<InvoiceIssuanceProcess?> GetProcessByIdAsync(Guid processId, CancellationToken cancellationToken) => Task.FromResult<InvoiceIssuanceProcess?>(process);
        public Task<InvoiceIssuanceProcess?> GetProcessByKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) => Task.FromResult<InvoiceIssuanceProcess?>(process);
        public void AddProcess(InvoiceIssuanceProcess value) { }
        public void AddOutbox(StockDeductionOutboxRequest request) { }
        public Task CommitAsync(CancellationToken cancellationToken) { CommitCount++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GuidGeneratorFake : IGuidGenerator { public Guid NewGuid() => Guid.NewGuid(); }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class TelemetryFake : IBillingTelemetry
    {
        public void InvoiceCreated() { }
        public void ItemOperation(string operation, string outcome) { }
        public void IssuanceRequested(string outcome) { }
        public void IssuanceTransitioned(string status) { }
        public void ProductCatalogRequest(string outcome) { }
    }
}
