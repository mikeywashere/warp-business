using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Catalog.Domain;

namespace WarpBusiness.Plugin.Catalog.Data.EfConfigs;

public class ProductOptionValueConfiguration : IEntityTypeConfiguration<ProductOptionValue>
{
    public void Configure(EntityTypeBuilder<ProductOptionValue> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Value).IsRequired().HasMaxLength(200);

        builder.HasIndex(v => new { v.ProductOptionId, v.Value }).IsUnique();

        builder.HasOne(v => v.ProductOption)
            .WithMany(o => o.Values)
            .HasForeignKey(v => v.ProductOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
