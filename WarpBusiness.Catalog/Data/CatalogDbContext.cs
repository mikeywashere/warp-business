using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Catalog.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<ProductTypeAttribute> ProductTypeAttributes => Set<ProductTypeAttribute>();
    public DbSet<CatalogAttributeType> AttributeTypes => Set<CatalogAttributeType>();
    public DbSet<CatalogAttributeOption> AttributeOptions => Set<CatalogAttributeOption>();
    public DbSet<ProductVariantAttributeValue> VariantAttributeValues => Set<ProductVariantAttributeValue>();
    public DbSet<CatalogNotation> Notations => Set<CatalogNotation>();
    public DbSet<ProductNotation> ProductNotations => Set<ProductNotation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique()
                .HasFilter("\"ParentCategoryId\" IS NULL")
                .HasDatabaseName("IX_Categories_TenantId_Name_Root");

            entity.HasIndex(e => new { e.TenantId, e.ParentCategoryId, e.Name })
                .IsUnique()
                .HasFilter("\"ParentCategoryId\" IS NOT NULL")
                .HasDatabaseName("IX_Categories_TenantId_Parent_Name");

            entity.HasOne(e => e.ParentCategory)
                .WithMany(e => e.SubCategories)
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<ProductTypeAttribute>(entity =>
        {
            entity.HasKey(e => new { e.ProductTypeId, e.AttributeTypeId });

            entity.HasOne(e => e.ProductType)
                .WithMany(pt => pt.Attributes)
                .HasForeignKey(e => e.ProductTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AttributeType)
                .WithMany(at => at.ProductTypeAttributes)
                .HasForeignKey(e => e.AttributeTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CatalogAttributeType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.ValueType).HasConversion<string>();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<CatalogAttributeOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AttributeTypeId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.AttributeTypeId, e.TenantId, e.Value }).IsUnique();

            entity.Property(e => e.Value).HasMaxLength(200).IsRequired();
            entity.Property(e => e.HexCode).HasMaxLength(7);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.AttributeType)
                .WithMany(at => at.Options)
                .HasForeignKey(e => e.AttributeTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductVariantAttributeValue>(entity =>
        {
            entity.HasKey(e => new { e.VariantId, e.AttributeTypeId });

            entity.Property(e => e.TextValue).HasMaxLength(1000);
            entity.Property(e => e.NumberValue).HasPrecision(18, 4);

            entity.HasOne(e => e.Variant)
                .WithMany(v => v.AttributeValues)
                .HasForeignKey(e => e.VariantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AttributeType)
                .WithMany(at => at.VariantValues)
                .HasForeignKey(e => e.AttributeTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AttributeOption)
                .WithMany(ao => ao.VariantValues)
                .HasForeignKey(e => e.AttributeOptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.SKU, e.TenantId })
                .IsUnique()
                .HasFilter("\"SKU\" IS NOT NULL");

            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Brand).HasMaxLength(200);
            entity.Property(e => e.SKU).HasMaxLength(100);
            entity.Property(e => e.BasePrice).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProductType)
                .WithMany(pt => pt.Products)
                .HasForeignKey(e => e.ProductTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CatalogNotation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();

            entity.Property(e => e.Name).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Icon)
                .HasConversion(new ValueConverter<NotationIcon?, string?>(
                    v => v.HasValue ? v.Value.ToString() : null,
                    v => NotationIconParser.ParseOrNull(v)))
                .HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.ToTable("Notations");
        });

        modelBuilder.Entity<ProductNotation>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.NotationId });

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Notations)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Notation)
                .WithMany(n => n.ProductNotations)
                .HasForeignKey(e => e.NotationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable("ProductNotations");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ProductId);

            entity.Property(e => e.SKU).HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => new { e.SKU, e.TenantId })
                .IsUnique()
                .HasFilter("\"SKU\" IS NOT NULL");

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductMedia>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ProductId).HasFilter("\"ProductId\" IS NOT NULL");
            entity.HasIndex(e => e.VariantId).HasFilter("\"VariantId\" IS NOT NULL");
            entity.Property(e => e.ObjectKey).HasMaxLength(500).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.MediaType).HasConversion<string>();

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Media)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Variant)
                .WithMany(v => v.Media)
                .HasForeignKey(e => e.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
