using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarpBusiness.Plugin.Invoicing.Domain;

namespace WarpBusiness.Plugin.Invoicing.Data.EfConfigs;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.HasKey(li => li.Id);

        builder.Property(li => li.Description).IsRequired().HasMaxLength(1000);
        builder.Property(li => li.ProductName).HasMaxLength(500);
        builder.Property(li => li.ProductSku).HasMaxLength(100);
        builder.Property(li => li.VariantDescription).HasMaxLength(500);
        builder.Property(li => li.EmployeeName).HasMaxLength(200);
        builder.Property(li => li.UnitOfMeasure).HasMaxLength(50);

        builder.Property(li => li.Quantity).HasPrecision(18, 4);
        builder.Property(li => li.UnitPrice).HasPrecision(18, 4);
        builder.Property(li => li.DiscountPercent).HasPrecision(8, 4);
        builder.Property(li => li.DiscountAmount).HasPrecision(18, 4);
        builder.Property(li => li.LineTotal).HasPrecision(18, 4);
        builder.Property(li => li.Hours).HasPrecision(8, 2);

        builder.Property(li => li.LineItemType).HasConversion<string>().HasMaxLength(50);

        builder.HasOne(li => li.Invoice)
            .WithMany(i => i.LineItems)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(li => new { li.InvoiceId, li.LineNumber });
        builder.HasIndex(li => li.ProductId).HasFilter("\"ProductId\" IS NOT NULL");
        builder.HasIndex(li => li.TimeEntryId).HasFilter("\"TimeEntryId\" IS NOT NULL");
    }
}
