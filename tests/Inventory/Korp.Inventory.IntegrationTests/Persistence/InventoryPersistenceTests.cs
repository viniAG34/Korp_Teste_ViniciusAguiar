using Korp.Inventory.Domain.Products;
using Korp.Inventory.Infrastructure.Persistence;
using Korp.Inventory.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Korp.Inventory.IntegrationTests.Persistence;

public sealed class InventoryPersistenceTests : IAsyncLifetime
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable("INVENTORY_TEST_CONNECTION")
        ?? "Host=localhost;Port=5433;Database=inventory_db;Username=inventory;Password=inventory_test_password";

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE stock_movements, products, inbox_messages, outbox_messages CASCADE", TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TstData001MigrationsCreateExpectedInventorySchema()
    {
        await using var context = CreateContext();
        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(applied, migration => migration.EndsWith("_InitialInventory", StringComparison.Ordinal));
        Assert.Equal(4, await CountApplicationTablesAsync(context));
    }

    [Fact]
    public async Task TstData005ProductConstraintsRejectDuplicateCode()
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        context.Products.Add(Product.Create(Guid.NewGuid(), "P-001", "First", 1, Guid.NewGuid(), now));
        context.Products.Add(Product.Create(Guid.NewGuid(), "p-001", "Second", 1, Guid.NewGuid(), now));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstData006ConcurrentLastUnitAllowsOnlyOneCommit()
    {
        var productId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var setup = CreateContext())
        {
            setup.Products.Add(Product.Create(productId, "LAST-1", "Last unit", 1, Guid.NewGuid(), now));
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstProduct = await first.Products.SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);
        var secondProduct = await second.Products.SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);
        first.StockMovements.Add(firstProduct.DeductForInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, now.AddSeconds(1)));
        second.StockMovements.Add(secondProduct.DeductForInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, now.AddSeconds(1)));

        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verification = CreateContext();
        Assert.Equal(0, await verification.Products.Where(product => product.Id == productId).Select(product => product.Balance).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.StockMovements.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstData008MovementUniquenessRejectsLogicalDuplicate()
    {
        var now = DateTimeOffset.UtcNow;
        var product = Product.Create(Guid.NewGuid(), "MOV-1", "Movement", 3, Guid.NewGuid(), now);
        var invoiceId = Guid.NewGuid();
        await using var context = CreateContext();
        context.Products.Add(product);
        context.StockMovements.Add(product.DeductForInvoice(Guid.NewGuid(), invoiceId, Guid.NewGuid(), 1, now.AddSeconds(1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var reloaded = await context.Products.SingleAsync(candidate => candidate.Id == product.Id, TestContext.Current.CancellationToken);
        context.StockMovements.Add(reloaded.DeductForInvoice(Guid.NewGuid(), invoiceId, Guid.NewGuid(), 1, now.AddSeconds(2)));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstData014InboxPrimaryKeyRejectsAlteredRedelivery()
    {
        var messageId = Guid.NewGuid();
        await using (var firstDelivery = CreateContext())
        {
            firstDelivery.InboxMessages.Add(InboxMessage.Create(messageId, "stock.requested", 1, Guid.NewGuid(), null, new string('A', 64), DateTimeOffset.UtcNow));
            await firstDelivery.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var alteredRedelivery = CreateContext();
        alteredRedelivery.InboxMessages.Add(InboxMessage.Create(messageId, "stock.requested", 1, Guid.NewGuid(), null, new string('B', 64), DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => alteredRedelivery.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstData015OutboxLeaseUsesOptimisticConcurrency()
    {
        var id = Guid.NewGuid();
        await using (var setup = CreateContext())
        {
            setup.OutboxMessages.Add(OutboxMessage.Create(id, "stock.completed", 1, "{}", Guid.NewGuid(), null, DateTimeOffset.UtcNow));
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstMessage = await first.OutboxMessages.SingleAsync(message => message.Id == id, TestContext.Current.CancellationToken);
        var secondMessage = await second.OutboxMessages.SingleAsync(message => message.Id == id, TestContext.Current.CancellationToken);
        firstMessage.AcquireLease(Guid.NewGuid(), firstMessage.NextAttemptAtUtc.AddMinutes(1));
        secondMessage.AcquireLease(Guid.NewGuid(), secondMessage.NextAttemptAtUtc.AddMinutes(1));

        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstData016OutboxFailureAndConfirmationPreserveRecoverableIntent()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var message = OutboxMessage.Create(Guid.NewGuid(), "stock.completed", 1, "{}", Guid.NewGuid(), null, occurredAt);
        message.AcquireLease(Guid.NewGuid(), occurredAt.AddMinutes(1));
        message.RecordFailure(" temporary failure ", occurredAt.AddSeconds(5));

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("temporary failure", message.LastError);
        Assert.Null(message.LockId);
        Assert.Null(message.PublishedAtUtc);

        message.AcquireLease(Guid.NewGuid(), occurredAt.AddMinutes(2));
        message.MarkPublished(occurredAt.AddSeconds(10));
        await using var context = CreateContext();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var persisted = await context.OutboxMessages.SingleAsync(candidate => candidate.Id == message.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted.PublishedAtUtc);
        Assert.Null(persisted.LockId);
    }

    [Fact]
    public async Task TstData018AuthorshipHasNoForeignKeyOutsideInventory()
    {
        await using var context = CreateContext();
        var foreignKeyCount = await context.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.table_constraints WHERE constraint_type = 'FOREIGN KEY' AND table_name = 'products'").SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, foreignKeyCount);
    }

    [Fact]
    public async Task TstData019SchemaContainsNoSpeculativeTables()
    {
        await using var context = CreateContext();
        var forbiddenCount = await context.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('suppliers','purchases','prices','taxes','payments','audit_logs','refresh_tokens','sessions')").SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, forbiddenCount);
    }

    private InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new InventoryDbContext(options);
    }

    private static Task<int> CountApplicationTablesAsync(InventoryDbContext context) =>
        context.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'").SingleAsync(TestContext.Current.CancellationToken);
}
