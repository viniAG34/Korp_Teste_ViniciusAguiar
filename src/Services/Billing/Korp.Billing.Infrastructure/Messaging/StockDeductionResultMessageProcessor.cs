using System.Security.Cryptography;
using System.Text.Json;
using Korp.Billing.Application.Common;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Domain.Issuance;
using Korp.Billing.Domain;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;

namespace Korp.Billing.Infrastructure.Messaging;

public sealed record StockDeductionResultDelivery(
    ReadOnlyMemory<byte> Body, string? MessageId, string? MessageType, string? CorrelationId,
    string? ContentType, string? ContentEncoding, int? MessageVersion, string? Producer);

public enum StockResultProcessingOutcome { Processed, Duplicate, EquivalentTerminal, DeterministicFailure }
public sealed record StockResultProcessingResult(StockResultProcessingOutcome Outcome, string? FailureCode = null);

public sealed class StockDeductionResultMessageProcessor(ApplyStockResultHandler handler)
{
    private static readonly HashSet<string> RejectionCodes = new(StringComparer.Ordinal)
    { "invalid_stock_deduction_request", "product_not_found", "insufficient_stock" };

    public async Task<StockResultProcessingResult> ProcessAsync(
        StockDeductionResultDelivery delivery, CancellationToken cancellationToken)
    {
        if (!ValidProperties(delivery)) return Failure("invalid_properties");
        try
        {
            return delivery.MessageType switch
            {
                IntegrationEventTypes.StockDeductionCompleted => await ProcessCompletedAsync(delivery, cancellationToken),
                IntegrationEventTypes.StockDeductionRejected => await ProcessRejectedAsync(delivery, cancellationToken),
                IntegrationEventTypes.StockDeductionProcessingFailed => await ProcessFailedAsync(delivery, cancellationToken),
                _ => Failure("unknown_message_type")
            };
        }
        catch (JsonException) { return Failure("invalid_json"); }
        catch (BillingMessageIntegrityException) { return Failure("message_integrity_violation"); }
        catch (BillingConsistencyException) { return Failure("inconsistent_result_target"); }
        catch (BillingResultContradictionException) { return Failure("contradictory_terminal_result"); }
        catch (DomainRuleException) { return Failure("invalid_result_contract"); }
    }

    private async Task<StockResultProcessingResult> ProcessRejectedAsync(
        StockDeductionResultDelivery delivery, CancellationToken cancellationToken)
    {
        var envelope = Deserialize<StockDeductionRejectedV1>(delivery);
        if (envelope is null || !RejectionCodes.Contains(envelope.Payload.ReasonCode)
            || !ValidDescription(envelope.Payload.ReasonDescription)) return Failure("invalid_result_contract");
        return await ApplyAsync(delivery, envelope, envelope.Payload.IssuanceProcessId, envelope.Payload.InvoiceId,
            IssuanceTransitionKind.Rejected, envelope.Payload.ReasonCode,
            envelope.Payload.ReasonDescription, cancellationToken);
    }

    private async Task<StockResultProcessingResult> ProcessFailedAsync(
        StockDeductionResultDelivery delivery, CancellationToken cancellationToken)
    {
        var envelope = Deserialize<StockDeductionProcessingFailedV1>(delivery);
        if (envelope is null || envelope.Payload.ReasonCode != "stock_processing_failed"
            || !ValidDescription(envelope.Payload.ReasonDescription)) return Failure("invalid_result_contract");
        return await ApplyAsync(delivery, envelope, envelope.Payload.IssuanceProcessId, envelope.Payload.InvoiceId,
            IssuanceTransitionKind.ManualIntervention, envelope.Payload.ReasonCode,
            envelope.Payload.ReasonDescription, cancellationToken);
    }

    private async Task<StockResultProcessingResult> ProcessCompletedAsync(
        StockDeductionResultDelivery delivery, CancellationToken cancellationToken)
    {
        var envelope = Deserialize<StockDeductionCompletedV1>(delivery);
        if (envelope is null) return Failure("invalid_result_contract");
        var payload = envelope.Payload;
        return await ApplyAsync(delivery, envelope, payload.IssuanceProcessId, payload.InvoiceId,
            IssuanceTransitionKind.Completed, null, null, cancellationToken);
    }

    private async Task<StockResultProcessingResult> ApplyAsync<T>(StockDeductionResultDelivery delivery,
        IntegrationEventEnvelope<T> envelope, Guid processId, Guid invoiceId, IssuanceTransitionKind kind,
        string? code, string? description, CancellationToken cancellationToken)
    {
        if (!ValidEnvelope(delivery, envelope) || processId == Guid.Empty || invoiceId == Guid.Empty)
            return Failure("invalid_envelope");
        var status = await handler.HandleAsync(new ApplyStockResultCommand(envelope.MessageId,
            envelope.MessageType, envelope.MessageVersion, envelope.CorrelationId, envelope.CausationId,
            Convert.ToHexString(SHA256.HashData(delivery.Body.Span)), processId, invoiceId,
            kind, code, description), cancellationToken);
        return new(status switch
        {
            ApplyStockResultStatus.Duplicate => StockResultProcessingOutcome.Duplicate,
            ApplyStockResultStatus.EquivalentTerminal => StockResultProcessingOutcome.EquivalentTerminal,
            _ => StockResultProcessingOutcome.Processed
        });
    }

    private static IntegrationEventEnvelope<T>? Deserialize<T>(StockDeductionResultDelivery delivery) =>
        JsonSerializer.Deserialize<IntegrationEventEnvelope<T>>(delivery.Body.Span, JsonSerializerOptions.Web);

    private static bool ValidProperties(StockDeductionResultDelivery delivery) =>
        delivery.MessageVersion == 1 && delivery.Producer == IntegrationEventProducers.Inventory
        && string.Equals(delivery.ContentType, "application/json", StringComparison.OrdinalIgnoreCase)
        && string.Equals(delivery.ContentEncoding, "utf-8", StringComparison.OrdinalIgnoreCase);

    private static bool ValidEnvelope<T>(StockDeductionResultDelivery delivery, IntegrationEventEnvelope<T> envelope) =>
        Guid.TryParse(delivery.MessageId, out var messageId) && messageId == envelope.MessageId
        && messageId != Guid.Empty && delivery.MessageType == envelope.MessageType
        && delivery.MessageVersion == envelope.MessageVersion && envelope.MessageVersion == 1
        && delivery.Producer == envelope.Producer && envelope.Producer == IntegrationEventProducers.Inventory
        && Guid.TryParse(delivery.CorrelationId, out var correlationId) && correlationId == envelope.CorrelationId
        && correlationId != Guid.Empty && envelope.OccurredAtUtc != default
        && envelope.OccurredAtUtc.Offset == TimeSpan.Zero && envelope.Payload is not null;

    private static bool ValidDescription(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= InvoiceIssuanceProcess.OutcomeDescriptionMaxLength;
    private static StockResultProcessingResult Failure(string code) =>
        new(StockResultProcessingOutcome.DeterministicFailure, code);
}
