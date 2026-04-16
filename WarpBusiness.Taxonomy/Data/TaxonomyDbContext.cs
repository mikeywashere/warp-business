using Microsoft.EntityFrameworkCore;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Data;

public class TaxonomyDbContext : DbContext
{
    public TaxonomyDbContext(DbContextOptions<TaxonomyDbContext> options) : base(options)
    {
    }

    public DbSet<TaxonomyNode> TaxonomyNodes => Set<TaxonomyNode>();
    public DbSet<ExternalTaxonomyCache> ExternalTaxonomyCaches => Set<ExternalTaxonomyCache>();
    public DbSet<ExternalTaxonomyNode> ExternalTaxonomyNodes => Set<ExternalTaxonomyNode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("taxonomy");

        modelBuilder.Entity<TaxonomyNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.ParentNodeId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.MaterializedPath });
            entity.HasIndex(e => new { e.TenantId, e.SourceProvider, e.SourceExternalId })
                .IsUnique()
                .HasFilter("\"SourceProvider\" IS NOT NULL");

            entity.Property(e => e.Name).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.MaterializedPath).HasMaxLength(1000);
            entity.Property(e => e.SourceProvider).HasMaxLength(100);
            entity.Property(e => e.SourceExternalId).HasMaxLength(100);
            entity.Property(e => e.SourcePath).HasMaxLength(1000);

            entity.HasOne(e => e.ParentNode)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExternalTaxonomyCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Provider);
            entity.HasIndex(e => new { e.Provider, e.DownloadedAt });

            entity.Property(e => e.Provider).HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.SourceVersion).HasMaxLength(100);
            entity.Property(e => e.SourceChecksum).HasMaxLength(64);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        });

        modelBuilder.Entity<ExternalTaxonomyNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CacheId);
            entity.HasIndex(e => new { e.Provider, e.ExternalId });
            entity.HasIndex(e => new { e.CacheId, e.ParentExternalId });

            entity.Property(e => e.Provider).HasMaxLength(100);
            entity.Property(e => e.ExternalId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ParentExternalId).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(300).IsRequired();
            entity.Property(e => e.FullPath).HasMaxLength(1000).IsRequired();

            entity.HasOne(e => e.Cache)
                .WithMany()
                .HasForeignKey(e => e.CacheId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
