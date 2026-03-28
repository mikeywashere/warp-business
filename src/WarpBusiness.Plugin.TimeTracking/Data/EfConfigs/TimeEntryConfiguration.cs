using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.TimeTracking.Domain;

namespace WarpBusiness.Plugin.TimeTracking.Data.EfConfigs;

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.EmployeeName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Hours).HasPrecision(8, 2);
        builder.Property(t => t.CompanyName).HasMaxLength(500);
        builder.Property(t => t.BillingRate).HasPrecision(18, 4);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.ApprovedById).HasMaxLength(450);
        builder.Property(t => t.RejectionReason).HasMaxLength(1000);
        builder.Property(t => t.CreatedBy).IsRequired().HasMaxLength(450);

        builder.HasOne(t => t.TimeEntryType)
            .WithMany(tt => tt.TimeEntries)
            .HasForeignKey(t => t.TimeEntryTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.TenantId, t.EmployeeId, t.Date });
        builder.HasIndex(t => new { t.TenantId, t.CompanyId }).HasFilter("\"CompanyId\" IS NOT NULL");
        builder.HasIndex(t => new { t.TenantId, t.Status });
    }
}
