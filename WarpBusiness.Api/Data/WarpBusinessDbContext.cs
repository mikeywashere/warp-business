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
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<TenantRequest> TenantRequests => Set<TenantRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("warp");

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

        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(3).IsFixedLength();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Symbol).HasMaxLength(10);
            entity.Property(e => e.NumericCode).HasMaxLength(3);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Slug).HasMaxLength(100);
            entity.Property(e => e.PreferredCurrencyCode).HasMaxLength(3);
            entity.Property(e => e.LoginTimeoutMinutes).HasDefaultValue(480);
            entity.Property(e => e.LogoMimeType).HasMaxLength(100);
            entity.Property(e => e.SubscriptionPlan).HasMaxLength(100);
            entity.Property(e => e.EnabledFeatures).HasMaxLength(2000);

            entity.HasOne(e => e.PreferredCurrency)
                .WithMany()
                .HasForeignKey(e => e.PreferredCurrencyCode)
                .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<TenantRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedToUserId)
                .HasFilter("\"AssignedToUserId\" IS NOT NULL");

            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Resolution).HasMaxLength(4000);
            entity.Property(e => e.AssignedToName).HasMaxLength(200);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AssignedTo)
                .WithMany()
                .HasForeignKey(e => e.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
