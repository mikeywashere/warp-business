using Microsoft.EntityFrameworkCore;

namespace WarpBusiness.Plugin.Crm.Data;

/// <summary>
/// DbContext for the CRM plugin. Uses the 'crm' schema in the same PostgreSQL database.
/// Entities are registered here after being moved from WarpBusiness.Api.
/// </summary>
public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    // Hicks will add DbSet<T> properties here when moving entities over
    // e.g.:
    // public DbSet<Contact> Contacts => Set<Contact>();
    // public DbSet<Company> Companies => Set<Company>();
    // public DbSet<Deal> Deals => Set<Deal>();
    // public DbSet<Activity> Activities => Set<Activity>();
    // public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    // public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("crm");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
