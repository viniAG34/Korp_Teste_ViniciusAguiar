using Korp.Billing.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korp.Billing.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", table =>
        {
            table.HasCheckConstraint("ck_invoices_number_positive", "number > 0");
            table.HasCheckConstraint("ck_invoices_status", "status IN ('open', 'closed')");
            table.HasCheckConstraint("ck_invoices_status_timestamps", "(status = 'open' AND closed_at_utc IS NULL) OR (status = 'closed' AND closed_at_utc IS NOT NULL AND is_issuance_in_progress = false)");
            table.HasCheckConstraint("ck_invoices_timestamps", "updated_at_utc >= created_at_utc AND (closed_at_utc IS NULL OR closed_at_utc >= created_at_utc)");
        });
        builder.HasKey(invoice => invoice.Id).HasName("pk_invoices");
        builder.Property(invoice => invoice.Number).HasDefaultValueSql("nextval('invoice_number_seq')").ValueGeneratedOnAdd();
        builder.Property(invoice => invoice.Status).HasConversion(value => value == InvoiceStatus.Open ? "open" : "closed", value => value == "open" ? InvoiceStatus.Open : InvoiceStatus.Closed).HasMaxLength(20).IsRequired();
        builder.Property(invoice => invoice.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(invoice => invoice.Number).IsUnique().HasDatabaseName("uq_invoices_number");
        builder.HasIndex(invoice => new { invoice.CreatedAtUtc, invoice.Id }).HasDatabaseName("ix_invoices_created_at_utc_id").IsDescending(true, false);
        builder.Navigation(invoice => invoice.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(invoice => invoice.Items).WithOne().HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_invoice_items_invoices");
    }
}
