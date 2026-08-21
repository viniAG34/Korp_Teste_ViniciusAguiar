using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korp.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BillingDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Billing database configuration is required.");

        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddDbContextFactory<BillingDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceReadService, InvoiceReadService>();
        services.AddScoped<IIssuanceProcessReadService, IssuanceProcessReadService>();
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        services.AddScoped<IBillingUnitOfWorkFactory, BillingUnitOfWorkFactory>();
        services.AddSingleton<IGuidGenerator, SystemGuidGenerator>();
        services.AddSingleton<IBillingTelemetry, NullBillingTelemetry>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateInvoiceHandler>();
        services.AddScoped<GetInvoiceByIdHandler>();
        services.AddScoped<ListInvoicesHandler>();
        services.AddScoped<AddInvoiceItemHandler>();
        services.AddScoped<UpdateInvoiceItemQuantityHandler>();
        services.AddScoped<RemoveInvoiceItemHandler>();
        services.AddScoped<PrintInvoiceHandler>();
        services.AddScoped<GetIssuanceProcessHandler>();
        services.AddScoped<TransitionInvoiceIssuanceHandler>();
        return services;
    }
}

public sealed class SystemGuidGenerator : IGuidGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
}

public sealed class NullBillingTelemetry : IBillingTelemetry
{
    public void InvoiceCreated() { }
    public void ItemOperation(string operation, string outcome) { }
    public void IssuanceRequested(string outcome) { }
    public void IssuanceTransitioned(string status) { }
    public void ProductCatalogRequest(string outcome) { }
}
