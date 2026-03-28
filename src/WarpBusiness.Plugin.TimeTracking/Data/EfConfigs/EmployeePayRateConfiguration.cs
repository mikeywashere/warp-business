using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.TimeTracking.Domain;

namespace WarpBusiness.Plugin.TimeTracking.Data.EfConfigs;

public class EmployeePayRateConfiguration : IEntityTypeConfiguration<EmployeePayRate>
{
    public void Configure(EntityTypeBuilder<EmployeePayRate> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EmployeeName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Rate).HasPrecision(18, 4);
        builder.Property(e => e.RateType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(450);

        builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.EffectiveDate });
    }
}
