using Korp.Inventory.Application.Common;

namespace Korp.Inventory.Infrastructure;

public sealed class SystemGuidGenerator : IGuidGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
}
