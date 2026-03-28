using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Invoicing.Domain;

namespace WarpBusiness.Plugin.Invoicing.Data.EfConfigs;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
        builder.Property(i => i.CompanyName).HasMaxLength(500);
        builder.Property(i => i.ContactName).HasMaxLength(200);
        builder.Property(i => i.BillingAddress).HasMaxLength(1000);
        builder.Property(i => i.ShippingAddress).HasMaxLength(1000);
        builder.Property(i => i.PaymentTerms).HasMaxLength(100);
        builder.Property(i => i.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.Property(i => i.CustomerNotes).HasMaxLength(2000);
        builder.Property(i => i.FooterText).HasMaxLength(2000);
        builder.Property(i => i.CreatedBy).IsRequired().HasMaxLength(450);

        builder.Property(i => i.Subtotal).HasPrecision(18, 4);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 4);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 4);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 4);
        builder.Property(i => i.AmountPaid).HasPrecision(18, 4);
        builder.Property(i => i.BalanceDue).HasPrecision(18, 4);
        builder.Property(i => i.DiscountPercent).HasPrecision(8, 4);
        builder.Property(i => i.DiscountFixed).HasPrecision(18, 4);
        builder.Property(i => i.TaxRate).HasPrecision(8, 4);

        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.CompanyId })
            .HasFilter("\"CompanyId\" IS NOT NULL");
        builder.HasIndex(i => new { i.TenantId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.DueDate });
        builder.HasIndex(i => new { i.TenantId, i.IssueDate });
    }
}
