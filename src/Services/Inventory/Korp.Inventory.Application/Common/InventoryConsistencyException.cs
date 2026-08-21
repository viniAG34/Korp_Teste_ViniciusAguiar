namespace Korp.Inventory.Application.Common;

public sealed class InventoryConsistencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
