using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WarpBusiness.Taxonomy.Data;

public class TaxonomyDbContextFactory : IDesignTimeDbContextFactory<TaxonomyDbContext>
{
    public TaxonomyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TaxonomyDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=warpbusiness;Username=postgres;Password=postgres");

        return new TaxonomyDbContext(optionsBuilder.Options);
    }
}
