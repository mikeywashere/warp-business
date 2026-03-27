using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Plugin.EmployeeManagement.Data;

public class EmployeeDbContextFactory : IDesignTimeDbContextFactory<EmployeeDbContext>
{
    public EmployeeDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=warpbusiness_dev;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<EmployeeDbContext>();
        optionsBuilder.UseNpgsql(connStr);
        return new EmployeeDbContext(optionsBuilder.Options, NullTenantContext.Instance);
    }
}
