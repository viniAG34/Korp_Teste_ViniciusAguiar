using Korp.Billing.Api.Correlation;
using Korp.Billing.Api.Errors;
using Korp.Billing.Api.Features.Invoices;
using Korp.Billing.Api.Features.Issuance;
using Korp.Billing.Api.Http;
using Korp.Billing.Api.Observability;
using Korp.Billing.Api.ProductCatalog;
using Korp.Billing.Api.Security;
using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Infrastructure;
using Korp.Billing.Infrastructure.ProductCatalog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => ApiJsonOptions.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BillingExceptionHandler>();
builder.Services.AddBillingSecurity(builder.Configuration);
builder.Services.AddBillingInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ForwardAuthorizationHandler>();
builder.Services.AddHttpClient<IProductCatalogClient, ProductCatalogClient>(client =>
{
    var baseAddress = builder.Configuration["Services:InventoryBaseUrl"];
    if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var address))
        throw new InvalidOperationException("Inventory service address is required.");
    client.BaseAddress = address;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
  .AddHttpMessageHandler<ForwardAuthorizationHandler>();
builder.Services.AddSingleton<BillingMetrics>();
builder.Services.AddSingleton<IBillingTelemetry>(services => services.GetRequiredService<BillingMetrics>());

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapInvoiceEndpoints();
app.MapIssuanceEndpoints();

app.Run();

public partial class Program;
