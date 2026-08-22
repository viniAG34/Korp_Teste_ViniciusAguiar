using System.Text;
using System.Text.Json;
using Korp.Billing.Application.Common;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;
using Korp.Billing.Infrastructure.Messaging;
using Korp.Billing.Infrastructure.Persistence;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;
using Microsoft.EntityFrameworkCore;

namespace Korp.Billing.IntegrationTests.Messaging;

public sealed class StockDeductionResultProcessorIntegrationTests : IAsyncLifetime
{
    private readonly string connectionString = Environment.GetEnvironmentVariable("BILLING_TEST_CONNECTION")
        ?? "Host=localhost;Port=5434;Database=billing_db;Username=billing;Password=billing_test_password";
    private DbContextOptions<BillingDbContext> options = null!;

    public async ValueTask InitializeAsync()
    {
        options = new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention().Options;
        await using var context = new BillingDbContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE invoice_items, invoice_issuance_processes, invoices, inbox_messages, outbox_messages CASCADE",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TstDst013CompletedBeforeAwaitingStockClosesAtomicallyAndDuplicateHasNoEffect()
    {
        var (invoiceId, processId) = await SeedActiveAsync();
        var messageId = Guid.NewGuid();
        var first = await ProcessAsync(messageId, IntegrationEventTypes.StockDeductionCompleted,
            new StockDeductionCompletedV1(processId, invoiceId));
        var duplicate = await ProcessAsync(messageId, IntegrationEventTypes.StockDeductionCompleted,
            new StockDeductionCompletedV1(processId, invoiceId));

        Assert.Equal(StockResultProcessingOutcome.Processed, first.Outcome);
        Assert.Equal(StockResultProcessingOutcome.Duplicate, duplicate.Outcome);
        await using var context = new BillingDbContext(options);
        var invoice = await context.Invoices.SingleAsync(x => x.Id == invoiceId, TestContext.Current.CancellationToken);
        var process = await context.InvoiceIssuanceProcesses.SingleAsync(x => x.Id == processId, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.False(invoice.IsIssuanceInProgress);
        Assert.Equal(InvoiceIssuanceProcessStatus.Completed, process.Status);
        Assert.Equal(1, await context.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstDst014EquivalentTerminalIsRecordedAndContradictionPreservesState()
    {
        var (invoiceId, processId) = await SeedActiveAsync();
        await ProcessAsync(Guid.NewGuid(), IntegrationEventTypes.StockDeductionRejected,
            new StockDeductionRejectedV1(processId, invoiceId, "insufficient_stock", "Insufficient stock"));
        var equivalent = await ProcessAsync(Guid.NewGuid(), IntegrationEventTypes.StockDeductionRejected,
            new StockDeductionRejectedV1(processId, invoiceId, "insufficient_stock", "Insufficient stock"));
        var contradiction = await ProcessAsync(Guid.NewGuid(), IntegrationEventTypes.StockDeductionCompleted,
            new StockDeductionCompletedV1(processId, invoiceId));

        Assert.Equal(StockResultProcessingOutcome.EquivalentTerminal, equivalent.Outcome);
        Assert.Equal(StockResultProcessingOutcome.DeterministicFailure, contradiction.Outcome);
        Assert.Equal("contradictory_terminal_result", contradiction.FailureCode);
        await using var context = new BillingDbContext(options);
        var invoice = await context.Invoices.SingleAsync(x => x.Id == invoiceId, TestContext.Current.CancellationToken);
        var process = await context.InvoiceIssuanceProcesses.SingleAsync(x => x.Id == processId, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.False(invoice.IsIssuanceInProgress);
        Assert.Equal(InvoiceIssuanceProcessStatus.Rejected, process.Status);
        Assert.Equal(2, await context.InboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessingFailedKeepsInvoiceBlockedForManualIntervention()
    {
        var (invoiceId, processId) = await SeedActiveAsync();
        var result = await ProcessAsync(Guid.NewGuid(), IntegrationEventTypes.StockDeductionProcessingFailed,
            new StockDeductionProcessingFailedV1(processId, invoiceId, "stock_processing_failed", "Manual review required"));

        Assert.Equal(StockResultProcessingOutcome.Processed, result.Outcome);
        await using var context = new BillingDbContext(options);
        var invoice = await context.Invoices.SingleAsync(x => x.Id == invoiceId, TestContext.Current.CancellationToken);
        var process = await context.InvoiceIssuanceProcesses.SingleAsync(x => x.Id == processId, TestContext.Current.CancellationToken);
        Assert.True(invoice.IsIssuanceInProgress);
        Assert.Equal(InvoiceIssuanceProcessStatus.ManualIntervention, process.Status);
        Assert.Equal("stock_processing_failed", process.OutcomeCode);
    }

    private async Task<(Guid InvoiceId, Guid ProcessId)> SeedActiveAsync()
    {
        await using var context = new BillingDbContext(options);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var invoice = Invoice.Create(Guid.NewGuid(), Random.Shared.NextInt64(1, long.MaxValue), Guid.NewGuid(), now);
        invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P-1", "Product", 1, now.AddSeconds(1));
        invoice.StartIssuance(now.AddSeconds(2));
        var process = InvoiceIssuanceProcess.Create(Guid.NewGuid(), invoice.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddSeconds(2));
        context.Invoices.Add(invoice); context.InvoiceIssuanceProcesses.Add(process);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (invoice.Id, process.Id);
    }

    private async Task<StockResultProcessingResult> ProcessAsync<T>(Guid messageId, string messageType, T payload)
    {
        var correlationId = messageId;
        var envelope = new IntegrationEventEnvelope<T>(messageId, messageType, 1,
            new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero),
            correlationId, null, IntegrationEventProducers.Inventory, payload);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web));
        var factory = new BillingUnitOfWorkFactory(new ContextFactory(options));
        var handler = new ApplyStockResultHandler(factory, TimeProvider.System, new Telemetry());
        var processor = new StockDeductionResultMessageProcessor(handler);
        return await processor.ProcessAsync(new(body, messageId.ToString(), messageType, correlationId.ToString(),
            "application/json", "utf-8", 1, IntegrationEventProducers.Inventory), TestContext.Current.CancellationToken);
    }

    private sealed class ContextFactory(DbContextOptions<BillingDbContext> options) : IDbContextFactory<BillingDbContext>
    { public BillingDbContext CreateDbContext() => new(options); }
    private sealed class Telemetry : IBillingTelemetry
    {
        public void InvoiceCreated() { } public void ItemOperation(string operation, string outcome) { }
        public void IssuanceRequested(string outcome) { } public void IssuanceTransitioned(string status) { }
        public void ProductCatalogRequest(string outcome) { }
    }
}
