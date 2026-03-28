using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Catalog.Domain;

namespace WarpBusiness.Plugin.Catalog.Data.EfConfigs;

public class VariantOptionValueConfiguration : IEntityTypeConfiguration<VariantOptionValue>
{
    public void Configure(EntityTypeBuilder<VariantOptionValue> builder)
    {
        builder.HasKey(v => new { v.ProductVariantId, v.ProductOptionValueId });

        builder.HasOne(v => v.ProductVariant)
            .WithMany(pv => pv.OptionValues)
            .HasForeignKey(v => v.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.ProductOptionValue)
            .WithMany(pov => pov.VariantOptionValues)
            .HasForeignKey(v => v.ProductOptionValueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
