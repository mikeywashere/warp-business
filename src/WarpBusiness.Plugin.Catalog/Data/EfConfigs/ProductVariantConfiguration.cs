using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Catalog.Domain;

namespace WarpBusiness.Plugin.Catalog.Data.EfConfigs;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Sku).HasMaxLength(100);
        builder.Property(v => v.Barcode).HasMaxLength(100);
        builder.Property(v => v.Price).HasPrecision(18, 4);
        builder.Property(v => v.CostPrice).HasPrecision(18, 4);
        builder.Property(v => v.Weight).HasPrecision(18, 4);

        builder.HasIndex(v => new { v.TenantId, v.Sku }).IsUnique()
            .HasFilter("\"Sku\" IS NOT NULL");

        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
