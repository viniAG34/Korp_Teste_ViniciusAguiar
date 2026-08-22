using System.Text;
using System.Text.Json;
using Korp.Integration.Contracts.Events;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Korp.Inventory.Infrastructure.Messaging;

public interface IOutboxPublisher : IAsyncDisposable
{
    Task PublishAsync(OutboxDelivery delivery, CancellationToken cancellationToken);
}

public sealed class RabbitMqOutboxPublisher(
    IRabbitMqConnection connection,
    IOptions<PublisherOptions> options) : IOutboxPublisher
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IChannel? channel;

    public async Task PublishAsync(OutboxDelivery delivery, CancellationToken cancellationToken)
    {
        ValidateEnvelope(delivery, IntegrationEventProducers.Inventory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ConfirmTimeoutSeconds));
        await gate.WaitAsync(timeout.Token);
        try
        {
            channel = await EnsureChannelAsync(timeout.Token);
            var properties = Properties(delivery, IntegrationEventProducers.Inventory);
            await channel.BasicPublishAsync(RabbitMqTopology.InventoryExchange,
                RabbitMqTopology.ResultRoutingKey, true, properties,
                Encoding.UTF8.GetBytes(delivery.Payload), timeout.Token);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (channel?.IsOpen == true) return channel;
        if (channel is not null) await channel.DisposeAsync();
        var current = await connection.GetAsync(cancellationToken);
        channel = await current.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true), cancellationToken);
        return channel;
    }

    private static BasicProperties Properties(OutboxDelivery delivery, string producer)
    {
        Dictionary<string, object?> headers = new()
        {
            ["x-message-version"] = delivery.SchemaVersion,
            ["x-producer"] = producer,
            ["x-retry-count"] = 0
        };
        if (delivery.CausationId is { } causationId) headers["x-causation-id"] = causationId.ToString("D");
        return new BasicProperties { Persistent = true, ContentType = "application/json", ContentEncoding = "utf-8",
            MessageId = delivery.Id.ToString("D"), Type = delivery.MessageType,
            CorrelationId = delivery.CorrelationId.ToString("D"), Headers = headers };
    }

    private static void ValidateEnvelope(OutboxDelivery delivery, string producer)
    {
        if (delivery.SchemaVersion != 1 || delivery.MessageType is not (
            IntegrationEventTypes.StockDeductionCompleted
            or IntegrationEventTypes.StockDeductionRejected
            or IntegrationEventTypes.StockDeductionProcessingFailed))
            throw new InvalidOperationException("Inventory Outbox contract is not supported.");
        using var document = JsonDocument.Parse(delivery.Payload);
        var root = document.RootElement;
        if (root.GetProperty("messageId").GetGuid() != delivery.Id
            || root.GetProperty("messageType").GetString() != delivery.MessageType
            || root.GetProperty("messageVersion").GetInt32() != delivery.SchemaVersion
            || (root.GetProperty("occurredAtUtc").GetDateTimeOffset() - delivery.OccurredAtUtc).Duration() > TimeSpan.FromMilliseconds(1)
            || root.GetProperty("correlationId").GetGuid() != delivery.CorrelationId
            || root.GetProperty("producer").GetString() != producer)
            throw new InvalidOperationException("Outbox envelope metadata is inconsistent.");
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null) await channel.DisposeAsync();
        gate.Dispose();
    }
}
