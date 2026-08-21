using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;
using Korp.Billing.Infrastructure.Persistence;
using Korp.Billing.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Korp.Billing.IntegrationTests.Persistence;

public sealed class BillingPersistenceTests : IAsyncLifetime
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable("BILLING_TEST_CONNECTION")
        ?? "Host=localhost;Port=5434;Database=billing_db;Username=billing;Password=billing_test_password";

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE invoice_items, invoice_issuance_processes, invoices, inbox_messages, outbox_messages CASCADE", TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TstData001MigrationsCreateExpectedBillingSchema()
    {
        await using var context = CreateContext();
        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        Assert.Contains(applied, migration => migration.EndsWith("_InitialBilling", StringComparison.Ordinal));

        var sequenceCount = await context.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.sequences WHERE sequence_name = 'invoice_number_seq'").SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, sequenceCount);
    }

    [Fact]
    public async Task TstData009SequenceProducesUniqueIncreasingNumbers()
    {
        await using var context = CreateContext();
        var first = await NextInvoiceNumberAsync(context);
        var second = await NextInvoiceNumberAsync(context);

        Assert.True(first > 0);
        Assert.True(second > first);
    }

    [Fact]
    public async Task TstData011InvoiceItemsRoundTripAndRootConcurrencyIsEnforced()
    {
        var invoiceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var setup = CreateContext())
        {
            var invoice = Invoice.Create(invoiceId, await NextInvoiceNumberAsync(setup), Guid.NewGuid(), now);
            invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P-1", "Product", 1, now.AddSeconds(1));
            setup.Invoices.Add(invoice);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstInvoice = await first.Invoices.Include(invoice => invoice.Items).SingleAsync(invoice => invoice.Id == invoiceId, TestContext.Current.CancellationToken);
        var secondInvoice = await second.Invoices.Include(invoice => invoice.Items).SingleAsync(invoice => invoice.Id == invoiceId, TestContext.Current.CancellationToken);
        firstInvoice.UpdateItemQuantity(firstInvoice.Items.Single().Id, 2, now.AddSeconds(2));
        secondInvoice.UpdateItemQuantity(secondInvoice.Items.Single().Id, 3, now.AddSeconds(2));

        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstData012ConcurrentActiveProcessesAreRejectedByDatabase()
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(Guid.NewGuid(), await NextInvoiceNumberAsync(context), Guid.NewGuid(), now);
        invoice.AddItem(Guid.NewGuid(), Guid.NewGuid(), "P-1", "Product", 1, now.AddSeconds(1));
        invoice.StartIssuance(now.AddSeconds(2));
        context.Invoices.Add(invoice);
        context.InvoiceIssuanceProcesses.Add(InvoiceIssuanceProcess.Create(Guid.NewGuid(), invoice.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddSeconds(2)));
        context.InvoiceIssuanceProcesses.Add(InvoiceIssuanceProcess.Create(Guid.NewGuid(), invoice.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddSeconds(2)));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstData014InboxAndTstData015OutboxPersistTechnicalGuarantees()
    {
        await using var context = CreateContext();
        var inboxId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();
        context.InboxMessages.Add(InboxMessage.Create(inboxId, "stock.completed", 1, Guid.NewGuid(), null, new string('A', 64), DateTimeOffset.UtcNow));
        var outbox = OutboxMessage.Create(outboxId, "stock.requested", 1, "{}", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        outbox.AcquireLease(Guid.NewGuid(), outbox.NextAttemptAtUtc.AddMinutes(1));
        context.OutboxMessages.Add(outbox);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await context.InboxMessages.CountAsync(message => message.MessageId == inboxId, TestContext.Current.CancellationToken));
        Assert.NotNull((await context.OutboxMessages.SingleAsync(message => message.Id == outboxId, TestContext.Current.CancellationToken)).LockId);
    }

    [Fact]
    public async Task TstData016OutboxFailureAndConfirmationPreserveRecoverableIntent()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var message = OutboxMessage.Create(Guid.NewGuid(), "stock.requested", 1, "{}", Guid.NewGuid(), null, occurredAt);
        message.AcquireLease(Guid.NewGuid(), occurredAt.AddMinutes(1));
        message.RecordFailure(new string('E', 1001), occurredAt.AddSeconds(5));

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal(1000, message.LastError?.Length);
        Assert.Null(message.LockId);
        Assert.Equal(occurredAt.AddSeconds(5), message.NextAttemptAtUtc);

        message.AcquireLease(Guid.NewGuid(), occurredAt.AddMinutes(2));
        message.MarkPublished(occurredAt.AddSeconds(10));

        await using var context = CreateContext();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var persisted = await context.OutboxMessages.SingleAsync(candidate => candidate.Id == message.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted.PublishedAtUtc);
        Assert.InRange(
            (persisted.PublishedAtUtc.Value - occurredAt.AddSeconds(10)).Duration(),
            TimeSpan.Zero,
            TimeSpan.FromMicroseconds(1));
        Assert.Null(persisted.LastError);
        Assert.Null(persisted.LockId);
    }

    [Fact]
    public async Task TstData013TerminalProcessRoundTripsWithoutOverwritingOutcome()
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(Guid.NewGuid(), await NextInvoiceNumberAsync(context), Guid.NewGuid(), now);
        context.Invoices.Add(invoice);
        var process = InvoiceIssuanceProcess.Create(Guid.NewGuid(), invoice.Id, Guid.NewGuid(), Guid.NewGuid(), now);
        process.Reject("insufficient_stock", "Safe outcome", now.AddSeconds(1));
        context.InvoiceIssuanceProcesses.Add(process);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var persisted = await context.InvoiceIssuanceProcesses.SingleAsync(candidate => candidate.Id == process.Id, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceIssuanceProcessStatus.Rejected, persisted.Status);
        Assert.Equal("insufficient_stock", persisted.OutcomeCode);
        Assert.NotNull(persisted.FinishedAtUtc);
    }

    [Fact]
    public async Task TstData018ExternalReferencesCreateNoCrossServiceForeignKeys()
    {
        await using var context = CreateContext();
        var forbiddenForeignKeys = await context.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.table_constraints tc JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name AND tc.constraint_schema = kcu.constraint_schema WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public' AND kcu.column_name IN ('product_id','created_by_user_id','requested_by_user_id')").SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, forbiddenForeignKeys);
    }

    [Fact]
    public async Task TstData019SchemaContainsNoSpeculativeFiscalTables()
    {
        await using var context = CreateContext();
        var forbiddenCount = await context.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('customers','orders','prices','taxes','payments','audit_logs')").SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, forbiddenCount);
    }

    private BillingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new BillingDbContext(options);
    }

    private static Task<long> NextInvoiceNumberAsync(BillingDbContext context) =>
        context.Database.SqlQueryRaw<long>("SELECT nextval('invoice_number_seq') AS \"Value\"").SingleAsync(TestContext.Current.CancellationToken);
}
