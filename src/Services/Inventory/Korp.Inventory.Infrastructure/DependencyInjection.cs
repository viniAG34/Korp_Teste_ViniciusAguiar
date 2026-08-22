using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Products;
using Korp.Inventory.Application.Stock;
using Korp.Inventory.Infrastructure.Persistence;
using Korp.Inventory.Infrastructure.Messaging;
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
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<PublisherOptions>()
            .Bind(configuration.GetSection(PublisherOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ConsumerOptions>()
            .Bind(configuration.GetSection(ConsumerOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<RabbitMqTopologyState>();
        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddSingleton<IOutboxStore, InventoryOutboxStore>();
        services.AddSingleton<IOutboxPublisher, RabbitMqOutboxPublisher>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddScoped<StockDeductionMessageProcessor>();
        services.AddSingleton<RabbitMqDeliveryForwarder>();
        services.AddHostedService<InventoryStockDeductionConsumer>();
        services.AddScoped<IInventoryUnitOfWorkFactory, InventoryUnitOfWorkFactory>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<GetProductSnapshotHandler>();
        services.AddScoped<ListProductsHandler>();
        services.AddScoped<DeductInvoiceStockHandler>();
        services.AddScoped<FinalizeStockDeductionFailureHandler>();
        return services;
    }
}
