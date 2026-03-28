using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Catalog.Domain;

namespace WarpBusiness.Plugin.Catalog.Data;

public class CatalogDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantOptionValue> VariantOptionValues => Set<VariantOptionValue>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Global tenant filters
        modelBuilder.Entity<Category>()
            .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Product>()
            .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProductOption>()
            .HasQueryFilter(o => o.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProductOptionValue>()
            .HasQueryFilter(v => v.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProductVariant>()
            .HasQueryFilter(v => v.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProductImage>()
            .HasQueryFilter(i => i.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProductIngredient>()
            .HasQueryFilter(i => i.TenantId == _tenantContext.TenantId);
    }
}
