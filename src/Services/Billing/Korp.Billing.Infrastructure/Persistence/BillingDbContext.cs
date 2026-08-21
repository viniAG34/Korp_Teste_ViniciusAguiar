using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;
using Korp.Billing.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Korp.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceIssuanceProcess> InvoiceIssuanceProcesses => Set<InvoiceIssuanceProcess>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("invoice_number_seq").StartsAt(1).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}
