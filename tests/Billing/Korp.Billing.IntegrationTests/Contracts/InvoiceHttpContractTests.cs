using System.Text.Json;
using Korp.Billing.Api.Features.Invoices.Contracts;
using Korp.Billing.Api.Features.Issuance.Contracts;
using Korp.Billing.Api.Http;

namespace Korp.Billing.IntegrationTests.Contracts;

public sealed class InvoiceHttpContractTests
{
    [Fact]
    public void InvoiceResponseUsesCanonicalStatusAndDoesNotExposeVersion()
    {
        var invoice = new InvoiceResponse(
            Guid.NewGuid(),
            42,
            InvoiceStatusResponse.Open,
            false,
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(invoice, ApiJsonOptions.Create()));

        Assert.Equal("open", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("version", out _));
        Assert.False(json.RootElement.TryGetProperty("closedAtUtc", out _));
    }

    [Fact]
    public void ItemRequestsCannotSupplySnapshotsOrIdentifiersOwnedByServer()
    {
        var addProperties = typeof(AddInvoiceItemRequest).GetProperties().Select(property => property.Name).ToArray();
        var updateProperties = typeof(UpdateInvoiceItemRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(["ProductId", "Quantity"], addProperties);
        Assert.Equal(["Quantity"], updateProperties);
    }

    [Fact]
    public void InvoicePageUsesSummaryWithoutLoadingAggregateItems()
    {
        var now = DateTimeOffset.UtcNow;
        var summary = new InvoiceSummaryResponse(
            Guid.NewGuid(),
            42,
            InvoiceStatusResponse.Open,
            false,
            1,
            now,
            now);
        var page = new InvoicePageResponse([summary], 1, 20, 1, 1);
        var item = new InvoiceItemResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "PROD-001",
            "Produto",
            2);
        var addRequest = new AddInvoiceItemRequest(item.ProductId, item.Quantity);
        var updateRequest = new UpdateInvoiceItemRequest(3);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(page, ApiJsonOptions.Create()));

        Assert.Equal(1, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(1, json.RootElement.GetProperty("items")[0].GetProperty("itemCount").GetInt32());
        Assert.Equal(2, addRequest.Quantity);
        Assert.Equal(3, updateRequest.Quantity);
    }

    [Fact]
    public void ProcessResponseSerializesSnakeCaseAndOmitsAbsentOutcome()
    {
        var process = new InvoiceIssuanceProcessResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceIssuanceProcessStatusResponse.ManualIntervention,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(process, ApiJsonOptions.Create()));

        Assert.Equal("manual_intervention", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("outcomeCode", out _));
        Assert.False(json.RootElement.TryGetProperty("outcomeDescription", out _));
    }
}
