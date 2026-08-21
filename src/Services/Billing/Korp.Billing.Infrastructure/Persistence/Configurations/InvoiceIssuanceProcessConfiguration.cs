using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korp.Billing.Infrastructure.Persistence.Configurations;

public sealed class InvoiceIssuanceProcessConfiguration : IEntityTypeConfiguration<InvoiceIssuanceProcess>
{
    public void Configure(EntityTypeBuilder<InvoiceIssuanceProcess> builder)
    {
        builder.ToTable("invoice_issuance_processes", table =>
        {
            table.HasCheckConstraint("ck_invoice_issuance_processes_status", "status IN ('pending', 'awaiting_stock', 'completed', 'rejected', 'manual_intervention')");
            table.HasCheckConstraint("ck_invoice_issuance_processes_outcome", "(status IN ('pending', 'awaiting_stock') AND finished_at_utc IS NULL AND outcome_code IS NULL AND outcome_description IS NULL) OR (status = 'completed' AND finished_at_utc IS NOT NULL AND outcome_code IS NULL AND outcome_description IS NULL) OR (status IN ('rejected', 'manual_intervention') AND finished_at_utc IS NOT NULL AND outcome_code IS NOT NULL)");
            table.HasCheckConstraint("ck_invoice_issuance_processes_timestamps", "updated_at_utc >= created_at_utc AND (finished_at_utc IS NULL OR finished_at_utc >= created_at_utc)");
        });
        builder.HasKey(process => process.Id).HasName("pk_invoice_issuance_processes");
        builder.Property(process => process.Status).HasConversion(value => ToDatabase(value), value => FromDatabase(value)).HasMaxLength(30).IsRequired();
        builder.Property(process => process.OutcomeCode).HasMaxLength(InvoiceIssuanceProcess.OutcomeCodeMaxLength);
        builder.Property(process => process.OutcomeDescription).HasMaxLength(InvoiceIssuanceProcess.OutcomeDescriptionMaxLength);
        builder.Property(process => process.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(process => process.InvoiceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_invoice_issuance_processes_invoices");
        builder.HasIndex(process => process.IdempotencyKey).IsUnique().HasDatabaseName("uq_invoice_issuance_processes_idempotency_key");
        builder.HasIndex(process => process.InvoiceId).IsUnique().HasFilter("status IN ('pending', 'awaiting_stock')").HasDatabaseName("uq_invoice_issuance_processes_active_invoice");
        builder.HasIndex(process => new { process.InvoiceId, process.CreatedAtUtc }).HasDatabaseName("ix_invoice_issuance_processes_invoice_id_created_at_utc").IsDescending(false, true);
        builder.HasIndex(process => new { process.Status, process.UpdatedAtUtc }).HasDatabaseName("ix_invoice_issuance_processes_status_updated_at_utc");
    }

    private static string ToDatabase(InvoiceIssuanceProcessStatus status) => status switch
    {
        InvoiceIssuanceProcessStatus.Pending => "pending",
        InvoiceIssuanceProcessStatus.AwaitingStock => "awaiting_stock",
        InvoiceIssuanceProcessStatus.Completed => "completed",
        InvoiceIssuanceProcessStatus.Rejected => "rejected",
        InvoiceIssuanceProcessStatus.ManualIntervention => "manual_intervention",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static InvoiceIssuanceProcessStatus FromDatabase(string status) => status switch
    {
        "pending" => InvoiceIssuanceProcessStatus.Pending,
        "awaiting_stock" => InvoiceIssuanceProcessStatus.AwaitingStock,
        "completed" => InvoiceIssuanceProcessStatus.Completed,
        "rejected" => InvoiceIssuanceProcessStatus.Rejected,
        "manual_intervention" => InvoiceIssuanceProcessStatus.ManualIntervention,
        _ => throw new InvalidOperationException("Unknown issuance status."),
    };
}
