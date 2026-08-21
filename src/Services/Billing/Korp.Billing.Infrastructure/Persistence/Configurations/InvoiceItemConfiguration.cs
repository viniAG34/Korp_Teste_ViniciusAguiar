using Korp.Billing.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korp.Billing.Infrastructure.Persistence.Configurations;

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("invoice_items", table =>
        {
            table.HasCheckConstraint("ck_invoice_items_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_invoice_items_product_code", "product_code <> '' AND product_code = btrim(product_code) AND product_code = upper(product_code)");
            table.HasCheckConstraint("ck_invoice_items_product_description", "product_description <> '' AND product_description = btrim(product_description)");
        });
        builder.HasKey(item => item.Id).HasName("pk_invoice_items");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ProductCode).HasMaxLength(InvoiceItem.ProductCodeMaxLength).IsRequired();
        builder.Property(item => item.ProductDescription).HasMaxLength(InvoiceItem.ProductDescriptionMaxLength).IsRequired();
        builder.HasIndex(item => new { item.InvoiceId, item.ProductId }).IsUnique().HasDatabaseName("uq_invoice_items_invoice_id_product_id");
    }
}
