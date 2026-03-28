using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Crm.Domain;

namespace WarpBusiness.Plugin.Crm.Data.EfConfigs;

public class ContactEmployeeRelationshipTypeConfiguration : IEntityTypeConfiguration<ContactEmployeeRelationshipType>
{
    public void Configure(EntityTypeBuilder<ContactEmployeeRelationshipType> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.DisplayOrder).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
    }
}
