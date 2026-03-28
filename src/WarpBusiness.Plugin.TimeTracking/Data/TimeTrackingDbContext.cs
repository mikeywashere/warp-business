using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.TimeTracking.Domain;

namespace WarpBusiness.Plugin.TimeTracking.Data;

public class TimeTrackingDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public TimeTrackingDbContext(DbContextOptions<TimeTrackingDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<TimeEntryType> TimeEntryTypes => Set<TimeEntryType>();
    public DbSet<EmployeePayRate> EmployeePayRates => Set<EmployeePayRate>();
    public DbSet<CustomerBillingRate> CustomerBillingRates => Set<CustomerBillingRate>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("timetracking");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeTrackingDbContext).Assembly);

        // Global tenant query filters
        modelBuilder.Entity<TimeEntryType>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<EmployeePayRate>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<CustomerBillingRate>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<TimeEntry>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }
}
