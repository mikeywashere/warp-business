using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Crm.Domain;

namespace WarpBusiness.Plugin.Crm.Data;

/// <summary>
/// DbContext for the CRM plugin. Uses the 'crm' schema in the same PostgreSQL database.
/// </summary>
public class CrmDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("crm");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Global tenant filters — every query is automatically scoped to the current tenant
        modelBuilder.Entity<Company>()
            .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Contact>()
            .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Deal>()
            .HasQueryFilter(d => d.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Activity>()
            .HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<CustomFieldDefinition>()
            .HasQueryFilter(f => f.TenantId == _tenantContext.TenantId);
    }
}
