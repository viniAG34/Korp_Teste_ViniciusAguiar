using System.Diagnostics.Metrics;
using Korp.Billing.Application.Common;

namespace Korp.Billing.Api.Observability;

public sealed class BillingMetrics : IBillingTelemetry, IDisposable
{
    private readonly Meter _meter = new("Korp.Billing");
    private readonly Counter<long> _invoicesCreated;
    private readonly Counter<long> _itemOperations;
    private readonly Counter<long> _issuanceRequests;
    private readonly Counter<long> _issuanceTransitions;
    private readonly Counter<long> _catalogRequests;

    public BillingMetrics()
    {
        _invoicesCreated = _meter.CreateCounter<long>("invoices_created_total");
        _itemOperations = _meter.CreateCounter<long>("invoice_item_operations_total");
        _issuanceRequests = _meter.CreateCounter<long>("invoice_issuance_requests_total");
        _issuanceTransitions = _meter.CreateCounter<long>("invoice_issuance_transitions_total");
        _catalogRequests = _meter.CreateCounter<long>("product_catalog_requests_total");
    }
    public void InvoiceCreated() => _invoicesCreated.Add(1);
    public void ItemOperation(string operation, string outcome) => _itemOperations.Add(1,
        new("operation", operation), new("outcome", outcome));
    public void IssuanceRequested(string outcome) => _issuanceRequests.Add(1,
        new KeyValuePair<string, object?>("outcome", outcome));
    public void IssuanceTransitioned(string status) => _issuanceTransitions.Add(1,
        new KeyValuePair<string, object?>("status", status));
    public void ProductCatalogRequest(string outcome) => _catalogRequests.Add(1,
        new KeyValuePair<string, object?>("outcome", outcome));
    public void Dispose() => _meter.Dispose();
}
