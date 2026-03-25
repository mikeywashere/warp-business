using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Api.Domain;

namespace WarpBusiness.Api.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).IsRequired().HasMaxLength(300);
        builder.Property(a => a.OwnerId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.CreatedBy).IsRequired().HasMaxLength(450);
        builder.HasOne(a => a.Contact)
            .WithMany(c => c.Activities)
            .HasForeignKey(a => a.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(a => a.Deal)
            .WithMany(d => d.Activities)
            .HasForeignKey(a => a.DealId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
