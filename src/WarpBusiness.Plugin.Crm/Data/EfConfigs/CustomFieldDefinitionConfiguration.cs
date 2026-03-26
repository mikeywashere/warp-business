using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Crm.Domain;

namespace WarpBusiness.Plugin.Crm.Data.EfConfigs;

public class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FieldType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.SelectOptions).HasMaxLength(2000);
        builder.HasIndex(x => new { x.EntityType, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.EntityType, x.DisplayOrder });
    }
}
