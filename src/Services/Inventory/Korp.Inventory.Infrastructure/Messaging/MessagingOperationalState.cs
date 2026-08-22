namespace Korp.Inventory.Infrastructure.Messaging;

public sealed class MessagingOperationalState
{
    private int topologyDeclared;
    private int topologyIncompatible;
    private int dispatcherRunning;
    private int consumerRunning;

    public bool IsTopologyDeclared => Volatile.Read(ref topologyDeclared) == 1;
    public bool IsTopologyIncompatible => Volatile.Read(ref topologyIncompatible) == 1;
    public bool IsDispatcherRunning => Volatile.Read(ref dispatcherRunning) == 1;
    public bool IsConsumerRunning => Volatile.Read(ref consumerRunning) == 1;

    public void SetTopologyDeclared(bool value) => Volatile.Write(ref topologyDeclared, value ? 1 : 0);
    public void SetTopologyIncompatible() => Volatile.Write(ref topologyIncompatible, 1);
    public void SetDispatcherRunning(bool value) => Volatile.Write(ref dispatcherRunning, value ? 1 : 0);
    public void SetConsumerRunning(bool value) => Volatile.Write(ref consumerRunning, value ? 1 : 0);
}
