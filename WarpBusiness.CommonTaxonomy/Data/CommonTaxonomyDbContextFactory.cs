using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WarpBusiness.CommonTaxonomy.Data;

public class CommonTaxonomyDbContextFactory : IDesignTimeDbContextFactory<CommonTaxonomyDbContext>
{
    public CommonTaxonomyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CommonTaxonomyDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=warpdb;Username=postgres;Password=postgres");
        return new CommonTaxonomyDbContext(optionsBuilder.Options);
    }
}
