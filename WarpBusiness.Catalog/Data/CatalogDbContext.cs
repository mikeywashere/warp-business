using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Catalog.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

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

            // Root categories: unique name per tenant
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique()
                .HasFilter("\"ParentCategoryId\" IS NULL")
                .HasDatabaseName("IX_Categories_TenantId_Name_Root");

            // Sub-categories: unique name within parent
            entity.HasIndex(e => new { e.TenantId, e.ParentCategoryId, e.Name })
                .IsUnique()
                .HasFilter("\"ParentCategoryId\" IS NOT NULL")
                .HasDatabaseName("IX_Categories_TenantId_Parent_Name");

            entity.HasOne(e => e.ParentCategory)
                .WithMany(e => e.SubCategories)
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Color>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.TenantId }).IsUnique();
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.HexCode).HasMaxLength(7).IsFixedLength(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Size>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.SizeType, e.TenantId }).IsUnique();
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SizeType).HasMaxLength(50).IsRequired().HasDefaultValue("General");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
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
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Variant SKU is unique within the tenant
            entity.HasIndex(e => new { e.SKU, e.TenantId })
                .IsUnique()
                .HasFilter("\"SKU\" IS NOT NULL");

            // At most one default (no color, no size) variant per product
            entity.HasIndex(e => e.ProductId)
                .IsUnique()
                .HasFilter("\"ColorId\" IS NULL AND \"SizeId\" IS NULL")
                .HasDatabaseName("IX_ProductVariants_ProductId_Default");

            // Unique color+size combination per product (both specified)
            entity.HasIndex(e => new { e.ProductId, e.ColorId, e.SizeId })
                .IsUnique()
                .HasFilter("\"ColorId\" IS NOT NULL AND \"SizeId\" IS NOT NULL")
                .HasDatabaseName("IX_ProductVariants_ProductId_Color_Size");

            // Unique color-only variants per product (no size)
            entity.HasIndex(e => new { e.ProductId, e.ColorId })
                .IsUnique()
                .HasFilter("\"ColorId\" IS NOT NULL AND \"SizeId\" IS NULL")
                .HasDatabaseName("IX_ProductVariants_ProductId_Color");

            // Unique size-only variants per product (no color)
            entity.HasIndex(e => new { e.ProductId, e.SizeId })
                .IsUnique()
                .HasFilter("\"ColorId\" IS NULL AND \"SizeId\" IS NOT NULL")
                .HasDatabaseName("IX_ProductVariants_ProductId_Size");

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Color)
                .WithMany(c => c.Variants)
                .HasForeignKey(e => e.ColorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Size)
                .WithMany(s => s.Variants)
                .HasForeignKey(e => e.SizeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
