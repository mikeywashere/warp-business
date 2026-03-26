using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Crm.Domain;

namespace WarpBusiness.Plugin.Crm.Data;

/// <summary>
/// DbContext for the CRM plugin. Uses the 'crm' schema in the same PostgreSQL database.
/// </summary>
public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
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
    }
}
