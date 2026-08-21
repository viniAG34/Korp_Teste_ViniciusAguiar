using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Korp.Inventory.Infrastructure.Persistence;

public sealed class InventoryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql("Host=localhost;Database=inventory_db;Username=inventory;Password=design_time_only")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new InventoryDbContext(options);
    }
}
