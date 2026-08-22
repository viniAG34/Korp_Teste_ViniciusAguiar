using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Korp.Inventory.Api.Health;

public static class HealthEndpointExtensions
{
    public static IEndpointRouteBuilder MapServiceHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", Options("live"));
        endpoints.MapHealthChecks("/health/ready", Options("ready"));
        endpoints.MapHealthChecks("/health/dependencies", Options("dependencies"));
        return endpoints;
    }

    private static HealthCheckOptions Options(string tag) => new()
    {
        Predicate = registration => registration.Tags.Contains(tag),
        ResponseWriter = WriteResponseAsync
    };

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            checks = report.Entries.OrderBy(entry => entry.Key).Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString()
            })
        }, cancellationToken: context.RequestAborted);
    }
}
