using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Catalog.Domain;

namespace WarpBusiness.Plugin.Catalog.Data.EfConfigs;

public class ProductIngredientConfiguration : IEntityTypeConfiguration<ProductIngredient>
{
    public void Configure(EntityTypeBuilder<ProductIngredient> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Quantity).HasMaxLength(100);
        builder.Property(i => i.Unit).HasMaxLength(50);
        builder.Property(i => i.AllergenType).HasMaxLength(100);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasIndex(i => new { i.ProductId, i.Name }).IsUnique();

        builder.HasOne(i => i.Product)
            .WithMany(p => p.Ingredients)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
