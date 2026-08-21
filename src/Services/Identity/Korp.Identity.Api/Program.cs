using Korp.Identity.Api.Errors;
using Korp.Identity.Api.Features.Auth;
using Korp.Identity.Api.Http;
using Korp.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => ApiJsonOptions.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<IdentityExceptionHandler>();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapLoginEndpoint();

app.Run();

public partial class Program;
