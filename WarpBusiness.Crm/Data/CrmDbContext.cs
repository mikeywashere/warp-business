using Microsoft.EntityFrameworkCore;
using WarpBusiness.Crm.Models;

namespace WarpBusiness.Crm.Data;

public class CrmDbContext : DbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerEmployee> CustomerEmployees => Set<CustomerEmployee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("crm");

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.Email, e.TenantId }).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.Industry).HasMaxLength(200);
            entity.Property(e => e.CompanySize).HasMaxLength(50);
            entity.Property(e => e.Website).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        });

        modelBuilder.Entity<CustomerEmployee>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(ce => ce.Customer)
                .WithMany(c => c.CustomerEmployees)
                .HasForeignKey(ce => ce.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.CustomerId, e.EmployeeId }).IsUnique();
            entity.HasIndex(e => new { e.CustomerId, e.BillingCurrency });

            entity.Property(e => e.Relationship).HasMaxLength(100).IsRequired();
            entity.Property(e => e.BillingRate).HasPrecision(18, 2);
            entity.Property(e => e.BillingCurrency).HasMaxLength(3).IsRequired();
        });
    }
}
