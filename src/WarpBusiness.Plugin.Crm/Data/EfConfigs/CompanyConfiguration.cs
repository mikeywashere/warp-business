using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Crm.Domain;

namespace WarpBusiness.Plugin.Crm.Data.EfConfigs;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Website).HasMaxLength(500);
        builder.Property(c => c.Industry).HasMaxLength(100);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(450);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
