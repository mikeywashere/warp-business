using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WarpBusiness.Crm.Data;

public class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=warpbusiness;Username=postgres;Password=postgres");
        
        return new CrmDbContext(optionsBuilder.Options);
    }
}
