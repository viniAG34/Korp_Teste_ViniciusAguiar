using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Korp.Billing.Infrastructure.Persistence;

public sealed class BillingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql("Host=localhost;Database=billing_db;Username=billing;Password=design_time_only")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new BillingDbContext(options);
    }
}
