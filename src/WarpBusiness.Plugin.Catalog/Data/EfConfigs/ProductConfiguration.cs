using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Catalog.Domain;

namespace WarpBusiness.Plugin.Catalog.Data.EfConfigs;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Description).HasMaxLength(10000);
        builder.Property(p => p.ShortDescription).HasMaxLength(1000);
        builder.Property(p => p.Slug).HasMaxLength(500);
        builder.Property(p => p.Sku).HasMaxLength(100);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.Brand).HasMaxLength(200);
        builder.Property(p => p.Manufacturer).HasMaxLength(200);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(p => p.BasePrice).HasPrecision(18, 4);
        builder.Property(p => p.CompareAtPrice).HasPrecision(18, 4);
        builder.Property(p => p.CostPrice).HasPrecision(18, 4);
        builder.Property(p => p.Weight).HasPrecision(18, 4);
        builder.Property(p => p.WeightUnit).HasMaxLength(20);
        builder.Property(p => p.Length).HasPrecision(18, 4);
        builder.Property(p => p.Width).HasPrecision(18, 4);
        builder.Property(p => p.Height).HasPrecision(18, 4);
        builder.Property(p => p.DimensionUnit).HasMaxLength(20);
        builder.Property(p => p.TaxCode).HasMaxLength(50);
        builder.Property(p => p.MetaTitle).HasMaxLength(200);
        builder.Property(p => p.MetaDescription).HasMaxLength(500);
        builder.Property(p => p.Tags).HasMaxLength(2000);
        builder.Property(p => p.CreatedBy).IsRequired().HasMaxLength(450);

        builder.Property(p => p.ProductType)
            .HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.Status)
            .HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(p => new { p.TenantId, p.Sku }).IsUnique()
            .HasFilter("\"Sku\" IS NOT NULL");
        builder.HasIndex(p => new { p.TenantId, p.Slug }).IsUnique()
            .HasFilter("\"Slug\" IS NOT NULL");
        builder.HasIndex(p => new { p.TenantId, p.Barcode }).IsUnique()
            .HasFilter("\"Barcode\" IS NOT NULL");

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
