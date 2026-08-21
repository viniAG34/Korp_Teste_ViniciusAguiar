namespace Korp.Inventory.Application.Common;

public sealed class InventoryServiceUnavailableException(Exception innerException)
    : Exception("Inventory persistence is unavailable.", innerException);
