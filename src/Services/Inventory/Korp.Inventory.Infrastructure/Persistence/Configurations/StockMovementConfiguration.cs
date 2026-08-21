using Korp.Inventory.Domain.StockMovements;
using Korp.Inventory.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korp.Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", table =>
        {
            table.HasCheckConstraint("ck_stock_movements_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_stock_movements_balances", "balance_before >= 0 AND balance_after >= 0 AND balance_after = balance_before - quantity");
            table.HasCheckConstraint("ck_stock_movements_type", "type = 'invoice_deduction'");
        });
        builder.HasKey(movement => movement.Id).HasName("pk_stock_movements");
        builder.Property(movement => movement.Type).HasConversion(value => "invoice_deduction", _ => StockMovementType.InvoiceDeduction).HasMaxLength(50).IsRequired();
        builder.HasOne<Product>().WithMany().HasForeignKey(movement => movement.ProductId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_movements_products");
        builder.HasIndex(movement => new { movement.EventId, movement.ProductId }).IsUnique().HasDatabaseName("uq_stock_movements_event_id_product_id");
        builder.HasIndex(movement => new { movement.InvoiceId, movement.ProductId }).IsUnique().HasDatabaseName("uq_stock_movements_invoice_id_product_id");
        builder.HasIndex(movement => new { movement.ProductId, movement.CreatedAtUtc }).HasDatabaseName("ix_stock_movements_product_id_created_at_utc").IsDescending(false, true);
    }
}
