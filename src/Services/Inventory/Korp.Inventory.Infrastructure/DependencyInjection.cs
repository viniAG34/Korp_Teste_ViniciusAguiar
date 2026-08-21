using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Products;
using Korp.Inventory.Application.Stock;
using Korp.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korp.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InventoryDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Inventory database configuration is required.");
        }

        services.AddDbContextFactory<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReadService, ProductReadService>();
        services.AddSingleton<IGuidGenerator, SystemGuidGenerator>();
        services.AddSingleton<IInventoryTelemetry, NullInventoryTelemetry>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IInventoryUnitOfWorkFactory, InventoryUnitOfWorkFactory>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<GetProductSnapshotHandler>();
        services.AddScoped<ListProductsHandler>();
        services.AddScoped<DeductInvoiceStockHandler>();
        return services;
    }
}
