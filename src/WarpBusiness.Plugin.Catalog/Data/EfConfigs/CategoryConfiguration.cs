using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Catalog.Domain;

namespace WarpBusiness.Plugin.Catalog.Data.EfConfigs;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Slug).HasMaxLength(200);
        builder.Property(c => c.ImageUrl).HasMaxLength(2000);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(450);

        builder.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique()
            .HasFilter("\"Slug\" IS NOT NULL");

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
