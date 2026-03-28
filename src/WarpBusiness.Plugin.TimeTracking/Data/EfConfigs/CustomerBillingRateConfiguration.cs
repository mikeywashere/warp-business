using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.TimeTracking.Domain;

namespace WarpBusiness.Plugin.TimeTracking.Data.EfConfigs;

public class CustomerBillingRateConfiguration : IEntityTypeConfiguration<CustomerBillingRate>
{
    public void Configure(EntityTypeBuilder<CustomerBillingRate> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.EmployeeName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CompanyName).IsRequired().HasMaxLength(500);
        builder.Property(c => c.HourlyRate).HasPrecision(18, 4);
        builder.Property(c => c.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(c => c.Notes).HasMaxLength(1000);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(450);

        builder.HasIndex(c => new { c.TenantId, c.EmployeeId, c.CompanyId, c.EffectiveDate });
    }
}
