using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Api.Identity.Tenancy;

namespace WarpBusiness.Api.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(63);
        builder.Property(t => t.DisplayName).HasMaxLength(200);
        builder.Property(t => t.CompanyImageContentType).HasMaxLength(100);
        // Slug must be globally unique (used as subdomain)
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.HasOne(t => t.SamlConfig)
            .WithOne(s => s.Tenant)
            .HasForeignKey<TenantSamlConfig>(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.UserTenants)
            .WithOne(ut => ut.Tenant)
            .HasForeignKey(ut => ut.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenantSamlConfigConfiguration : IEntityTypeConfiguration<TenantSamlConfig>
{
    public void Configure(EntityTypeBuilder<TenantSamlConfig> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.MetadataUrl).IsRequired().HasMaxLength(2000);
        builder.Property(s => s.EntityId).IsRequired().HasMaxLength(500);
    }
}

public class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.HasKey(ut => new { ut.UserId, ut.TenantId });
        builder.Property(ut => ut.UserId).IsRequired().HasMaxLength(450);
        builder.Property(ut => ut.Role).IsRequired().HasMaxLength(50);

        builder.HasOne(ut => ut.User)
            .WithMany()
            .HasForeignKey(ut => ut.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
