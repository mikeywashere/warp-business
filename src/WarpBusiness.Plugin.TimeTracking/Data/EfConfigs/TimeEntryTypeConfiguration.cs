using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.TimeTracking.Domain;

namespace WarpBusiness.Plugin.TimeTracking.Data.EfConfigs;

public class TimeEntryTypeConfiguration : IEntityTypeConfiguration<TimeEntryType>
{
    public void Configure(EntityTypeBuilder<TimeEntryType> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.CreatedBy).IsRequired().HasMaxLength(450);

        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.DisplayOrder });
    }
}
