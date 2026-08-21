using System.Diagnostics.Metrics;
using Korp.Inventory.Application.Common;

namespace Korp.Inventory.Api.Observability;

public sealed class InventoryMetrics : IInventoryTelemetry, IDisposable
{
    private readonly Meter _meter = new("Korp.Inventory");
    private readonly Counter<long> _productsCreated;
    private readonly Counter<long> _stockDeductions;
    private readonly Counter<long> _concurrencyConflicts;
    private readonly Histogram<double> _deductionDuration;

    public InventoryMetrics()
    {
        _productsCreated = _meter.CreateCounter<long>("products_created_total");
        _stockDeductions = _meter.CreateCounter<long>("stock_deductions_total");
        _concurrencyConflicts = _meter.CreateCounter<long>("stock_deduction_concurrency_conflicts_total");
        _deductionDuration = _meter.CreateHistogram<double>("stock_deduction_duration_seconds", "s");
    }

    public void ProductCreated() => _productsCreated.Add(1);
    public void RecordStockDeduction(string outcome, TimeSpan duration)
    {
        _stockDeductions.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        _deductionDuration.Record(duration.TotalSeconds);
    }

    public void RecordConcurrencyConflict() => _concurrencyConflicts.Add(1);
    public void Dispose() => _meter.Dispose();
}
