using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Models;

namespace WarpBusiness.Api.Data;

public class WarpBusinessDbContext : DbContext
{
    public WarpBusinessDbContext(DbContextOptions<WarpBusinessDbContext> options) : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.KeycloakSubjectId).IsUnique()
                .HasFilter("\"KeycloakSubjectId\" != ''");
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.KeycloakSubjectId).HasMaxLength(256);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<UserTenantMembership>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.TenantId });

            entity.HasOne(e => e.User)
                .WithMany(u => u.TenantMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.UserMemberships)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
