using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Invoicing.Domain;

namespace WarpBusiness.Plugin.Invoicing.Data.EfConfigs;

public class InvoiceSettingsConfiguration : IEntityTypeConfiguration<InvoiceSettings>
{
    public void Configure(EntityTypeBuilder<InvoiceSettings> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Prefix).IsRequired().HasMaxLength(20);
        builder.Property(s => s.DefaultPaymentTerms).IsRequired().HasMaxLength(100);
        builder.Property(s => s.DefaultCurrency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(s => s.DefaultTaxRate).HasPrecision(8, 4);
        builder.Property(s => s.DefaultFooterText).HasMaxLength(2000);
        builder.Property(s => s.DefaultCustomerNotes).HasMaxLength(2000);
        builder.Property(s => s.CompanyName).HasMaxLength(500);
        builder.Property(s => s.CompanyAddress).HasMaxLength(1000);
        builder.Property(s => s.CompanyPhone).HasMaxLength(50);
        builder.Property(s => s.CompanyEmail).HasMaxLength(200);
        builder.Property(s => s.CompanyLogoUrl).HasMaxLength(2000);
        builder.Property(s => s.UpdatedBy).IsRequired().HasMaxLength(450);

        builder.HasIndex(s => s.TenantId).IsUnique();
    }
}
