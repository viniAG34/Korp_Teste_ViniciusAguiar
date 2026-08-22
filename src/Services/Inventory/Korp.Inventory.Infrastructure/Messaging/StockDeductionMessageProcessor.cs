using System.Security.Cryptography;
using System.Text.Json;
using Korp.Inventory.Application.Common;
using Korp.Inventory.Application.Stock;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;

namespace Korp.Inventory.Infrastructure.Messaging;

public sealed record StockDeductionDelivery(
    ReadOnlyMemory<byte> Body,
    string? MessageId,
    string? MessageType,
    string? CorrelationId,
    string? ContentType,
    string? ContentEncoding,
    int? MessageVersion,
    string? Producer);

public enum StockDeductionProcessingOutcome { Processed, Duplicate, DeterministicFailure }

public sealed record StockDeductionProcessingResult(
    StockDeductionProcessingOutcome Outcome,
    string? FailureCode = null);

public sealed class StockDeductionMessageProcessor(
    DeductInvoiceStockHandler handler,
    FinalizeStockDeductionFailureHandler terminalFailureHandler)
{
    public async Task<StockDeductionProcessingResult> ProcessAsync(
        StockDeductionDelivery delivery,
        CancellationToken cancellationToken)
    {
        IntegrationEventEnvelope<StockDeductionRequestedV1>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<StockDeductionRequestedV1>>(
                delivery.Body.Span, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return Deterministic("invalid_json");
        }

        if (envelope is null || !Valid(delivery, envelope)) return Deterministic("invalid_envelope");
        var hash = Convert.ToHexString(SHA256.HashData(delivery.Body.Span));
        try
        {
            var result = await handler.HandleAsync(new DeductInvoiceStockCommand(
                envelope.MessageId, envelope.Payload.IssuanceProcessId, envelope.Payload.InvoiceId,
                envelope.Payload.Items.Select(item => new DeductInvoiceStockItem(item.ProductId, item.Quantity)).ToArray(),
                envelope.CorrelationId, hash), cancellationToken);
            return new(result.Status == DeductionStatus.Duplicate
                ? StockDeductionProcessingOutcome.Duplicate
                : StockDeductionProcessingOutcome.Processed);
        }
        catch (InventoryMessageIntegrityException)
        {
            return Deterministic("message_integrity_violation");
        }
        catch (InventoryLogicalDivergenceException)
        {
            return Deterministic("logical_content_divergence");
        }
    }

    public async Task<TerminalFailureStatus> FinalizeFailureAsync(
        StockDeductionDelivery delivery, CancellationToken cancellationToken)
    {
        IntegrationEventEnvelope<StockDeductionRequestedV1>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<StockDeductionRequestedV1>>(
                delivery.Body.Span, JsonSerializerOptions.Web);
        }
        catch (JsonException) { return TerminalFailureStatus.Inconclusive; }
        if (envelope is null || !Valid(delivery, envelope)) return TerminalFailureStatus.Inconclusive;
        var hash = Convert.ToHexString(SHA256.HashData(delivery.Body.Span));
        return await terminalFailureHandler.HandleAsync(new DeductInvoiceStockCommand(envelope.MessageId,
            envelope.Payload.IssuanceProcessId, envelope.Payload.InvoiceId,
            envelope.Payload.Items.Select(item => new DeductInvoiceStockItem(item.ProductId, item.Quantity)).ToArray(),
            envelope.CorrelationId, hash), cancellationToken);
    }

    private static bool Valid(StockDeductionDelivery delivery, IntegrationEventEnvelope<StockDeductionRequestedV1> envelope) =>
        Guid.TryParse(delivery.MessageId, out var propertyMessageId)
        && propertyMessageId == envelope.MessageId
        && envelope.MessageId != Guid.Empty
        && delivery.MessageType == IntegrationEventTypes.StockDeductionRequested
        && delivery.MessageType == envelope.MessageType
        && envelope.MessageVersion == 1
        && delivery.MessageVersion == envelope.MessageVersion
        && envelope.Producer == IntegrationEventProducers.Billing
        && delivery.Producer == envelope.Producer
        && Guid.TryParse(delivery.CorrelationId, out var propertyCorrelationId)
        && propertyCorrelationId == envelope.CorrelationId
        && envelope.CorrelationId != Guid.Empty
        && string.Equals(delivery.ContentType, "application/json", StringComparison.OrdinalIgnoreCase)
        && string.Equals(delivery.ContentEncoding, "utf-8", StringComparison.OrdinalIgnoreCase)
        && envelope.OccurredAtUtc != default
        && envelope.OccurredAtUtc.Offset == TimeSpan.Zero
        && envelope.Payload is not null
        && envelope.Payload.IssuanceProcessId != Guid.Empty
        && envelope.Payload.InvoiceId != Guid.Empty
        && envelope.Payload.RequestedByUserId != Guid.Empty
        && envelope.Payload.Items is not null;

    private static StockDeductionProcessingResult Deterministic(string code) =>
        new(StockDeductionProcessingOutcome.DeterministicFailure, code);
}
