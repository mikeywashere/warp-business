using Microsoft.EntityFrameworkCore;
using WarpBusiness.CommonTaxonomy.Models;

namespace WarpBusiness.CommonTaxonomy.Data;

public class CommonTaxonomyDbContext : DbContext
{
    public CommonTaxonomyDbContext(DbContextOptions<CommonTaxonomyDbContext> options) : base(options) { }

    public DbSet<TaxonomyProvider> Providers => Set<TaxonomyProvider>();
    public DbSet<TaxonomyNode> Nodes => Set<TaxonomyNode>();
    public DbSet<TaxonomyNodeAttribute> NodeAttributes => Set<TaxonomyNodeAttribute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("common_taxonomy");

        modelBuilder.Entity<TaxonomyProvider>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.LastDownloadChecksum).HasMaxLength(64);
        });

        modelBuilder.Entity<TaxonomyNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => new { e.ProviderId, e.ExternalId }).IsUnique();
            entity.Property(e => e.ExternalId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.FullPath).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();

            entity.HasOne(e => e.Provider)
                .WithMany(p => p.Nodes)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Parent)
                .WithMany(n => n.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaxonomyNodeAttribute>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NodeId);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ValueType).HasConversion<string>();
            entity.Property(e => e.AllowedValues).HasMaxLength(4000);
            entity.Property(e => e.Unit).HasMaxLength(50);

            entity.HasOne(e => e.Node)
                .WithMany(n => n.Attributes)
                .HasForeignKey(e => e.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
