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
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();
    public DbSet<VariantOptionValue> VariantOptionValues => Set<VariantOptionValue>();
    public DbSet<ProductTaxonomyMapping> ProductTaxonomyMappings => Set<ProductTaxonomyMapping>();
    public DbSet<ProductTaxonomyAttributeValue> ProductTaxonomyAttributeValues => Set<ProductTaxonomyAttributeValue>();
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
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ProductId);

            entity.Property(e => e.SKU).HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.PriceAdjustmentType).HasConversion<string>().HasDefaultValue(PriceAdjustmentType.None);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => new { e.SKU, e.TenantId })
                .IsUnique()
                .HasFilter("\"SKU\" IS NOT NULL");

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.ProductId, e.Name }).IsUnique();

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IsVariantAxis).HasDefaultValue(true);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Options)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductOptionValue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OptionId);
            entity.HasIndex(e => new { e.OptionId, e.Value }).IsUnique();

            entity.Property(e => e.Value).HasMaxLength(200).IsRequired();
            entity.Property(e => e.HexCode).HasMaxLength(7);

            entity.HasOne(e => e.Option)
                .WithMany(o => o.Values)
                .HasForeignKey(e => e.OptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VariantOptionValue>(entity =>
        {
            entity.HasKey(e => new { e.VariantId, e.OptionId });

            entity.HasOne(e => e.Variant)
                .WithMany(v => v.OptionValues)
                .HasForeignKey(e => e.VariantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Option)
                .WithMany()
                .HasForeignKey(e => e.OptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OptionValue)
                .WithMany(ov => ov.VariantValues)
                .HasForeignKey(e => e.OptionValueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductTaxonomyMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.ProductId, e.ProviderKey, e.TaxonomyNodeId }).IsUnique();

            entity.Property(e => e.ProviderKey).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NodeName).HasMaxLength(500).IsRequired();
            entity.Property(e => e.NodeFullPath).HasMaxLength(2000).IsRequired();

            entity.HasOne(e => e.Product)
                .WithMany(p => p.TaxonomyMappings)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductTaxonomyAttributeValue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MappingId);
            entity.HasIndex(e => new { e.MappingId, e.AttributeId }).IsUnique();

            entity.Property(e => e.AttributeName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TextValue).HasMaxLength(1000);
            entity.Property(e => e.NumberValue).HasPrecision(18, 4);

            entity.HasOne(e => e.Mapping)
                .WithMany(m => m.AttributeValues)
                .HasForeignKey(e => e.MappingId)
                .OnDelete(DeleteBehavior.Cascade);
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