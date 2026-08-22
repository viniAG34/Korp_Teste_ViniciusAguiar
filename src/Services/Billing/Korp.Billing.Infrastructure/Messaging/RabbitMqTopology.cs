namespace Korp.Billing.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public const string BillingExchange = "korp.billing.v1";
    public const string InventoryExchange = "korp.inventory.v1";
    public const string RetryExchange = "korp.retry.v1";
    public const string DeadLetterExchange = "korp.dead-letter.v1";
    public const string RequestRoutingKey = "stock.deduction.requested.v1";
    public const string ResultRoutingKey = "stock.deduction.result.v1";
    public const string InventoryQueue = "korp.inventory.stock-deduction.v1";
    public const string BillingQueue = "korp.billing.stock-deduction-results.v1";
    public const string InventoryDeadLetterQueue = "korp.inventory.stock-deduction.dlq.v1";
    public const string BillingDeadLetterQueue = "korp.billing.stock-deduction-results.dlq.v1";

    public static readonly RetryQueueDefinition[] RetryQueues =
    [
        new("korp.inventory.stock-deduction.retry-5s.v1", "inventory.stock-deduction.retry.5s.v1", 5_000, BillingExchange, RequestRoutingKey),
        new("korp.inventory.stock-deduction.retry-30s.v1", "inventory.stock-deduction.retry.30s.v1", 30_000, BillingExchange, RequestRoutingKey),
        new("korp.inventory.stock-deduction.retry-120s.v1", "inventory.stock-deduction.retry.120s.v1", 120_000, BillingExchange, RequestRoutingKey),
        new("korp.billing.stock-deduction-results.retry-5s.v1", "billing.stock-deduction-result.retry.5s.v1", 5_000, InventoryExchange, ResultRoutingKey),
        new("korp.billing.stock-deduction-results.retry-30s.v1", "billing.stock-deduction-result.retry.30s.v1", 30_000, InventoryExchange, ResultRoutingKey),
        new("korp.billing.stock-deduction-results.retry-120s.v1", "billing.stock-deduction-result.retry.120s.v1", 120_000, InventoryExchange, ResultRoutingKey)
    ];

    public sealed record RetryQueueDefinition(string Name, string RoutingKey, int TtlMilliseconds, string ReturnExchange, string ReturnRoutingKey);
}
