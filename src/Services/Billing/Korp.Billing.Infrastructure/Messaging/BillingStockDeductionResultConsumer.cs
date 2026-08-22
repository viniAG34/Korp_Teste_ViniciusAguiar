using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Korp.Billing.Infrastructure.Messaging;

public sealed class BillingRabbitMqDeliveryForwarder(
    IRabbitMqConnection connection, IOptions<PublisherOptions> publisherOptions) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IChannel? channel;

    public async Task ForwardAsync(BasicDeliverEventArgs delivery, bool deterministic, CancellationToken cancellationToken)
    {
        var retry = HeaderInt(delivery.BasicProperties.Headers, "x-retry-count");
        var dead = deterministic || retry >= 3;
        var routingKey = dead ? "billing.stock-deduction-result.dead.v1" : retry switch
        {
            0 => "billing.stock-deduction-result.retry.5s.v1",
            1 => "billing.stock-deduction-result.retry.30s.v1",
            _ => "billing.stock-deduction-result.retry.120s.v1"
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(publisherOptions.Value.ConfirmTimeoutSeconds));
        await gate.WaitAsync(timeout.Token);
        try
        {
            channel = await EnsureChannelAsync(timeout.Token);
            var headers = delivery.BasicProperties.Headers?.ToDictionary(x => x.Key, x => x.Value)
                ?? new Dictionary<string, object?>();
            headers["x-retry-count"] = dead ? retry : retry + 1;
            headers["x-original-queue"] = RabbitMqTopology.BillingQueue;
            var properties = new BasicProperties
            {
                Persistent = true, ContentType = delivery.BasicProperties.ContentType,
                ContentEncoding = delivery.BasicProperties.ContentEncoding,
                MessageId = delivery.BasicProperties.MessageId, Type = delivery.BasicProperties.Type,
                CorrelationId = delivery.BasicProperties.CorrelationId, Headers = headers
            };
            await channel.BasicPublishAsync(dead ? RabbitMqTopology.DeadLetterExchange : RabbitMqTopology.RetryExchange,
                routingKey, true, properties, delivery.Body, timeout.Token);
        }
        finally { gate.Release(); }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (channel?.IsOpen == true) return channel;
        if (channel is not null) await channel.DisposeAsync();
        channel = await (await connection.GetAsync(cancellationToken)).CreateChannelAsync(
            new CreateChannelOptions(true, true), cancellationToken);
        return channel;
    }

    internal static int HeaderInt(IDictionary<string, object?>? headers, string name)
    {
        if (headers is null || !headers.TryGetValue(name, out var value) || value is null) return 0;
        return value switch
        {
            byte number => number, short number => number, int number => number,
            long number => checked((int)number),
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var number) => number,
            _ => 0
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null) await channel.DisposeAsync();
        gate.Dispose();
    }
}

public sealed partial class BillingStockDeductionResultConsumer(
    IRabbitMqConnection connection, RabbitMqTopologyState topology, IServiceScopeFactory scopeFactory,
    BillingRabbitMqDeliveryForwarder forwarder, IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<ConsumerOptions> consumerOptions, ILogger<BillingStockDeductionResultConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rabbitOptions.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested && !topology.IsIncompatible)
        {
            if (!topology.IsDeclared) { await Task.Delay(500, stoppingToken); continue; }
            try
            {
                var current = await connection.GetAsync(stoppingToken);
                await using var channel = await current.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, consumerOptions.Value.PrefetchCount, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) => await HandleAsync(channel, delivery, stoppingToken);
                var tag = await channel.BasicConsumeAsync(RabbitMqTopology.BillingQueue, false, consumer, stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                await channel.BasicCancelAsync(tag, false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                Log.ConsumerFailed(logger, exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(rabbitOptions.Value.NetworkRecoveryIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var headers = delivery.BasicProperties.Headers;
            var request = new StockDeductionResultDelivery(delivery.Body, delivery.BasicProperties.MessageId,
                delivery.BasicProperties.Type, delivery.BasicProperties.CorrelationId,
                delivery.BasicProperties.ContentType, delivery.BasicProperties.ContentEncoding,
                BillingRabbitMqDeliveryForwarder.HeaderInt(headers, "x-message-version"),
                HeaderString(headers, "x-producer"));
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<StockDeductionResultMessageProcessor>()
                .ProcessAsync(request, cancellationToken);
            if (result.Outcome == StockResultProcessingOutcome.DeterministicFailure)
                await forwarder.ForwardAsync(delivery, true, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
            Log.MessageProcessed(logger, delivery.BasicProperties.MessageId ?? string.Empty, result.Outcome.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            try
            {
                await forwarder.ForwardAsync(delivery, false, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                Log.MessageRetried(logger, delivery.BasicProperties.MessageId ?? string.Empty,
                    BillingRabbitMqDeliveryForwarder.HeaderInt(delivery.BasicProperties.Headers, "x-retry-count") + 1);
            }
            catch
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, false, true, cancellationToken); throw;
            }
        }
    }

    private static string? HeaderString(IDictionary<string, object?>? headers, string name)
    {
        if (headers is null || !headers.TryGetValue(name, out var value) || value is null) return null;
        return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value.ToString();
    }

    private static partial class Log
    {
        [LoggerMessage(7101, LogLevel.Information, "Billing result processed. Event={Event} MessageId={MessageId} Outcome={Outcome}")]
        public static partial void MessageProcessed(ILogger logger, string messageId, string outcome, string @event = "message_processed");
        [LoggerMessage(7102, LogLevel.Warning, "Billing result scheduled for retry. Event={Event} MessageId={MessageId} Attempt={Attempt}")]
        public static partial void MessageRetried(ILogger logger, string messageId, int attempt, string @event = "message_retried");
        [LoggerMessage(7103, LogLevel.Warning, "Billing consumer cycle failed. Event={Event} FailureType={FailureType}")]
        public static partial void ConsumerFailed(ILogger logger, string failureType, string @event = "rabbitmq_connection_changed");
    }
}
