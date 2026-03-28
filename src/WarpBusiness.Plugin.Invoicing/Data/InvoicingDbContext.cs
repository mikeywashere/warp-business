using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Invoicing.Domain;

namespace WarpBusiness.Plugin.Invoicing.Data;

public class InvoicingDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public InvoicingDbContext(DbContextOptions<InvoicingDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<InvoiceSettings> InvoiceSettings => Set<InvoiceSettings>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("invoicing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoicingDbContext).Assembly);

        modelBuilder.Entity<Invoice>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<InvoiceLineItem>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<InvoicePayment>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<InvoiceSettings>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }
}
