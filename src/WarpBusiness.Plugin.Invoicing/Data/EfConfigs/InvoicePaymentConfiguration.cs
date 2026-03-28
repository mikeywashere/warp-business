using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Invoicing.Domain;

namespace WarpBusiness.Plugin.Invoicing.Data.EfConfigs;

public class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(p => p.ReferenceNumber).HasMaxLength(200);
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.Property(p => p.CreatedBy).IsRequired().HasMaxLength(450);

        builder.Property(p => p.Amount).HasPrecision(18, 4);

        builder.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(50);

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.InvoiceId, p.PaymentDate });
    }
}
