namespace Korp.Inventory.Application.Common;

public interface IInventoryTelemetry
{
    void RecordStockDeduction(string outcome, TimeSpan duration);
    void RecordConcurrencyConflict();
}
