using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Korp.Inventory.Infrastructure.Messaging;

public interface IRabbitMqConnection : IAsyncDisposable
{
    bool IsOpen { get; }
    Task<IConnection> GetAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IRabbitMqConnection
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;

    public bool IsOpen => connection?.IsOpen == true;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (connection?.IsOpen == true) return connection;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (connection?.IsOpen == true) return connection;
            if (connection is not null) await connection.DisposeAsync();

            var value = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = value.Host,
                Port = value.Port,
                VirtualHost = value.VirtualHost,
                UserName = value.Username,
                Password = value.Password,
                RequestedHeartbeat = TimeSpan.FromSeconds(value.RequestedHeartbeatSeconds),
                NetworkRecoveryInterval = TimeSpan.FromSeconds(value.NetworkRecoveryIntervalSeconds),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = false,
                ConsumerDispatchConcurrency = 1
            };
            connection = await factory.CreateConnectionAsync("korp-inventory", cancellationToken);
            return connection;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (connection is not null) await connection.DisposeAsync();
            connection = null;
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }
}
