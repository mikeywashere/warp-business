using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Crm.Domain;

namespace WarpBusiness.Plugin.Crm.Data.EfConfigs;

public class ContactEmployeeRelationshipConfiguration : IEntityTypeConfiguration<ContactEmployeeRelationship>
{
    public void Configure(EntityTypeBuilder<ContactEmployeeRelationship> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.EmployeeName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.EmployeeEmail).HasMaxLength(256);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(450);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasOne(r => r.Contact)
            .WithMany(c => c.EmployeeRelationships)
            .HasForeignKey(r => r.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.RelationshipType)
            .WithMany(t => t.Relationships)
            .HasForeignKey(r => r.RelationshipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ContactId);
        builder.HasIndex(r => r.RelationshipTypeId);
        builder.HasIndex(r => new { r.ContactId, r.EmployeeId, r.RelationshipTypeId }).IsUnique();
    }
}
