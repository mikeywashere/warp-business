using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Api.Domain;

namespace WarpBusiness.Api.Data.Configurations;

public class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasMaxLength(4000);
        builder.HasIndex(x => new { x.ContactId, x.FieldDefinitionId }).IsUnique();
        builder.HasOne(x => x.FieldDefinition)
            .WithMany(x => x.Values)
            .HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Contact)
            .WithMany(x => x.CustomFieldValues)
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
