using Korp.Inventory.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korp.Inventory.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
        {
            table.HasCheckConstraint("ck_products_code_format", "code <> '' AND code = btrim(code) AND code = upper(code) AND code ~ '^[A-Z0-9._-]+$'");
            table.HasCheckConstraint("ck_products_description", "description <> '' AND description = btrim(description)");
            table.HasCheckConstraint("ck_products_balance_non_negative", "balance >= 0");
            table.HasCheckConstraint("ck_products_timestamps", "updated_at_utc >= created_at_utc");
        });
        builder.HasKey(product => product.Id).HasName("pk_products");
        builder.Property(product => product.Code).HasConversion(code => code.Value, value => ProductCode.Create(value)).HasMaxLength(ProductCode.MaxLength).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(Product.DescriptionMaxLength).IsRequired();
        builder.Property(product => product.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(product => product.Code).IsUnique().HasDatabaseName("uq_products_code");
    }
}
