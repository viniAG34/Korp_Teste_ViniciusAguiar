using Korp.Inventory.Application.Stock;
using Korp.Inventory.Domain.Products;
using Korp.Inventory.Infrastructure;
using Korp.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korp.Inventory.IntegrationTests.Stock;

public sealed class StockDeductionIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable("INVENTORY_TEST_CONNECTION")
        ?? "Host=localhost;Port=5433;Database=inventory_db;Username=inventory;Password=inventory_test_password";
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:InventoryDatabase"] = _connectionString
        }).Build();
        _provider = new ServiceCollection()
            .AddInventoryInfrastructure(configuration)
            .BuildServiceProvider();
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE stock_movements, products, inbox_messages, outbox_messages CASCADE",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    [Fact]
    public async Task TstInv014MultipleProductsCommitBalancesAndMovementsAtomically()
    {
        var products = await SeedProductsAsync(("P-1", 5), ("P-2", 7));
        var command = Command(Guid.NewGuid(),
            new DeductInvoiceStockItem(products[0].Id, 2),
            new DeductInvoiceStockItem(products[1].Id, 3));

        var result = await HandleAsync(command);

        Assert.Equal(DeductionStatus.Completed, result.Status);
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var balances = await context.Products.OrderBy(product => product.Code)
            .Select(product => product.Balance).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Collection(balances, balance => Assert.Equal(3, balance), balance => Assert.Equal(4, balance));
        Assert.Equal(2, await context.StockMovements.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstInv015And016RejectionsProduceNoPartialEffect()
    {
        var products = await SeedProductsAsync(("P-1", 2), ("P-2", 1));
        var missing = await HandleAsync(Command(Guid.NewGuid(),
            new DeductInvoiceStockItem(products[0].Id, 1),
            new DeductInvoiceStockItem(Guid.NewGuid(), 1)));
        var insufficient = await HandleAsync(Command(Guid.NewGuid(),
            new DeductInvoiceStockItem(products[0].Id, 1),
            new DeductInvoiceStockItem(products[1].Id, 2)));

        Assert.Equal(DeductionReason.ProductNotFound, missing.Reason);
        Assert.Equal(DeductionReason.InsufficientStock, insufficient.Reason);
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var balances = await context.Products.OrderBy(product => product.Code)
            .Select(product => product.Balance).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Collection(balances, balance => Assert.Equal(2, balance), balance => Assert.Equal(1, balance));
        Assert.Equal(0, await context.StockMovements.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstInv019And020EquivalentIntentDoesNotDeductTwice()
    {
        var product = (await SeedProductsAsync(("P-1", 3))).Single();
        var invoiceId = Guid.NewGuid();

        var first = await HandleAsync(Command(invoiceId, new DeductInvoiceStockItem(product.Id, 1)));
        var repeated = await HandleAsync(Command(invoiceId, new DeductInvoiceStockItem(product.Id, 1)));

        Assert.Equal(DeductionStatus.Completed, first.Status);
        Assert.Equal(DeductionStatus.Completed, repeated.Status);
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(2, await context.Products.Select(candidate => candidate.Balance)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.StockMovements.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstInv021DivergentIntentIsTechnicalInconsistency()
    {
        var product = (await SeedProductsAsync(("P-1", 3))).Single();
        var invoiceId = Guid.NewGuid();
        await HandleAsync(Command(invoiceId, new DeductInvoiceStockItem(product.Id, 1)));

        await Assert.ThrowsAsync<Korp.Inventory.Application.Common.InventoryConsistencyException>(() =>
            HandleAsync(Command(invoiceId, new DeductInvoiceStockItem(product.Id, 2))));
    }

    private async Task<Product[]> SeedProductsAsync(params (string Code, int Balance)[] values)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var now = DateTimeOffset.UtcNow;
        var products = values.Select(value => Product.Create(
            Guid.NewGuid(), value.Code, value.Code, value.Balance, Guid.NewGuid(), now)).ToArray();
        context.Products.AddRange(products);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return products;
    }

    private async Task<DeductionResult> HandleAsync(DeductInvoiceStockCommand command)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DeductInvoiceStockHandler>()
            .HandleAsync(command, TestContext.Current.CancellationToken);
    }

    private static DeductInvoiceStockCommand Command(
        Guid invoiceId,
        params DeductInvoiceStockItem[] items) =>
        new(Guid.NewGuid(), Guid.NewGuid(), invoiceId, items);
}
