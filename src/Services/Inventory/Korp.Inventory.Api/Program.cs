using Korp.Inventory.Api.Errors;
using Korp.Inventory.Api.Features.Products;
using Korp.Inventory.Api.Http;
using Korp.Inventory.Api.Observability;
using Korp.Inventory.Api.Security;
using Korp.Inventory.Application.Common;
using Korp.Inventory.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => ApiJsonOptions.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<InventoryExceptionHandler>();
builder.Services.AddInventorySecurity(builder.Configuration);
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddSingleton<InventoryMetrics>();
builder.Services.AddSingleton<IInventoryTelemetry>(services =>
    services.GetRequiredService<InventoryMetrics>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapProductEndpoints();

app.Run();

public partial class Program;
