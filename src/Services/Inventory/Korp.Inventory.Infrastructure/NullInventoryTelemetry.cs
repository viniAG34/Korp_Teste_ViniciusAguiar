using Korp.Inventory.Application.Common;

namespace Korp.Inventory.Infrastructure;

public sealed class NullInventoryTelemetry : IInventoryTelemetry
{
    public void RecordStockDeduction(string outcome, TimeSpan duration)
    {
    }

    public void RecordConcurrencyConflict()
    {
    }
}
