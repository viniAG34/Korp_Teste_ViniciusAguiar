using System.Text.Json;
using System.Text.Json.Serialization;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;

namespace Korp.Integration.Contracts.UnitTests.Events;

public sealed class StockDeductionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void RequestedFixtureMatchesCanonicalContract()
    {
        var envelope = ReadFixture<StockDeductionRequestedV1>("stock-deduction-requested-v1.json");

        Assert.Equal(IntegrationEventTypes.StockDeductionRequested, envelope.MessageType);
        Assert.Equal(IntegrationEventProducers.Billing, envelope.Producer);
        Assert.Equal(1, envelope.MessageVersion);
        Assert.Null(envelope.CausationId);
        Assert.Single(envelope.Payload.Items);
        Assert.Equal(2, envelope.Payload.Items[0].Quantity);
    }

    [Fact]
    public void CompletedFixturePreservesCorrelationAndDirectCause()
    {
        var envelope = ReadFixture<StockDeductionCompletedV1>("stock-deduction-completed-v1.json");

        Assert.Equal(IntegrationEventTypes.StockDeductionCompleted, envelope.MessageType);
        Assert.Equal(IntegrationEventProducers.Inventory, envelope.Producer);
        Assert.NotNull(envelope.CausationId);
        Assert.NotEqual(envelope.MessageId, envelope.CausationId);
        Assert.NotEqual(Guid.Empty, envelope.CorrelationId);
    }

    [Fact]
    public void RejectedFixtureContainsOnlySafeFunctionalFailureData()
    {
        var envelope = ReadFixture<StockDeductionRejectedV1>("stock-deduction-rejected-v1.json");

        Assert.Equal(IntegrationEventTypes.StockDeductionRejected, envelope.MessageType);
        Assert.Equal(StockDeductionReasonCodes.InsufficientStock, envelope.Payload.ReasonCode);
        var failure = Assert.Single(envelope.Payload.Failures!);
        Assert.Equal(2, failure.RequestedQuantity);
        Assert.Equal(1, failure.AvailableBalance);
    }

    [Fact]
    public void ProcessingFailedFixtureContainsNoTechnicalDetails()
    {
        var envelope = ReadFixture<StockDeductionProcessingFailedV1>("stock-deduction-processing-failed-v1.json");
        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        Assert.Equal(IntegrationEventTypes.StockDeductionProcessingFailed, envelope.MessageType);
        Assert.Equal(StockDeductionReasonCodes.ProcessingFailed, envelope.Payload.ReasonCode);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("host", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("queue", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsumerIgnoresAnAdditionalOptionalProperty()
    {
        var original = File.ReadAllText(FixturePath("stock-deduction-completed-v1.json"));
        var evolved = original.Replace(
            "\"invoiceId\": \"771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7\"",
            "\"invoiceId\": \"771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7\", \"optionalDiagnostic\": \"safe\"",
            StringComparison.Ordinal);

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<StockDeductionCompletedV1>>(evolved, JsonOptions);

        Assert.NotNull(envelope);
        Assert.Equal(Guid.Parse("771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7"), envelope.Payload.InvoiceId);
    }

    [Fact]
    public void NullOptionalFieldsAreOmittedWhenSerialized()
    {
        var payload = new StockDeductionRejectedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StockDeductionReasonCodes.InvalidRequest,
            "Solicitação inválida.");
        var envelope = new IntegrationEventEnvelope<StockDeductionRejectedV1>(
            Guid.NewGuid(),
            IntegrationEventTypes.StockDeductionRejected,
            1,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Guid.NewGuid(),
            IntegrationEventProducers.Inventory,
            payload);

        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        Assert.DoesNotContain("failures", json, StringComparison.Ordinal);
    }

    private static IntegrationEventEnvelope<TPayload> ReadFixture<TPayload>(string fixtureName)
    {
        var json = File.ReadAllText(FixturePath(fixtureName));
        return JsonSerializer.Deserialize<IntegrationEventEnvelope<TPayload>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Fixture {fixtureName} could not be deserialized.");
    }

    private static string FixturePath(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
}
