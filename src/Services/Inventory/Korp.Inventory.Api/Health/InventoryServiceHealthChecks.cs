using Korp.Inventory.Infrastructure.Messaging;
using Korp.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Korp.Inventory.Api.Health;

public static class InventoryServiceHealthChecks
{
    public static IHealthChecksBuilder AddInventoryHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            .AddCheck<LocalConfigurationHealthCheck>("configuration", tags: ["ready"])
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready", "dependencies"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["dependencies"])
            .AddCheck<TopologyHealthCheck>("topology", tags: ["dependencies"])
            .AddCheck<DispatcherHealthCheck>("dispatcher", tags: ["dependencies"])
            .AddCheck<ConsumerHealthCheck>("consumer", tags: ["dependencies"]);
}

internal sealed class LocalConfigurationHealthCheck(IOptions<RabbitMqOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        _ = options.Value;
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}

internal sealed class DatabaseHealthCheck(IDbContextFactory<InventoryDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            if (!await database.Database.CanConnectAsync(cancellationToken)) return HealthCheckResult.Unhealthy();
            return (await database.Database.GetPendingMigrationsAsync(cancellationToken)).Any()
                ? HealthCheckResult.Unhealthy()
                : HealthCheckResult.Healthy();
        }
        catch { return HealthCheckResult.Unhealthy(); }
    }
}

internal sealed class RabbitMqHealthCheck(IRabbitMqConnection connection, IOptions<RabbitMqOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(options.Value.Enabled && connection.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
}

internal sealed class TopologyHealthCheck(MessagingOperationalState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.IsTopologyDeclared && !state.IsTopologyIncompatible ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
}

internal sealed class DispatcherHealthCheck(MessagingOperationalState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.IsDispatcherRunning ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
}

internal sealed class ConsumerHealthCheck(MessagingOperationalState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.IsConsumerRunning ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
}
