using Korp.Inventory.Api.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => ApiJsonOptions.Configure(options.SerializerOptions));

var app = builder.Build();

app.MapOpenApi();

app.Run();

public partial class Program;
