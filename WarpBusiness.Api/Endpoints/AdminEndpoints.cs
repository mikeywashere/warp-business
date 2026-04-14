using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Crm.Data;
using WarpBusiness.Employees.Data;

namespace WarpBusiness.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization("SystemAdministrator");

        // Export all data as a JSON backup payload.
        // Call this first, save the response, then call /clear.
        admin.MapPost("/export", ExportData)
            .WithName("AdminExport");

        // Truncate all data tables in the warp/employees/crm schemas.
        // Does NOT clear migration history tables.
        // Restart the application afterwards to reseed default data.
        admin.MapPost("/clear", ClearData)
            .WithName("AdminClear");
    }

    private static async Task<IResult> ExportData(
        WarpBusinessDbContext warpDb,
        EmployeeDbContext employeeDb,
        CrmDbContext crmDb)
    {
        var backup = new
        {
            ExportedAt = DateTime.UtcNow,
            Warp = new
            {
                Users = await warpDb.Users.AsNoTracking().ToListAsync(),
                Tenants = await warpDb.Tenants.AsNoTracking().ToListAsync(),
                UserTenantMemberships = await warpDb.UserTenantMemberships.AsNoTracking().ToListAsync(),
                Currencies = await warpDb.Currencies.AsNoTracking().ToListAsync(),
            },
            Employees = await employeeDb.Employees.AsNoTracking().ToListAsync(),
            Crm = new
            {
                Customers = await crmDb.Customers.AsNoTracking()
                    .Select(c => new
                    {
                        c.Id, c.TenantId, c.Name, c.Email, c.Phone, c.Address, c.City, c.State,
                        c.PostalCode, c.Country, c.Industry, c.CompanySize, c.Website, c.Notes,
                        c.IsActive, c.Currency, c.LogoMimeType, c.CreatedAt, c.UpdatedAt,
                        // Logo is base64-encoded in JSON; omit large blobs from backup summary
                        HasLogo = c.Logo != null
                    })
                    .ToListAsync(),
                CustomerEmployees = await crmDb.CustomerEmployees.AsNoTracking().ToListAsync(),
            }
        };

        return Results.Ok(backup);
    }

    private static async Task<IResult> ClearData(
        WarpBusinessDbContext warpDb,
        EmployeeDbContext employeeDb,
        CrmDbContext crmDb)
    {
        // Count rows before clearing for summary
        var counts = new Dictionary<string, int>
        {
            ["warp.Users"] = await warpDb.Users.CountAsync(),
            ["warp.Tenants"] = await warpDb.Tenants.CountAsync(),
            ["warp.UserTenantMemberships"] = await warpDb.UserTenantMemberships.CountAsync(),
            ["warp.Currencies"] = await warpDb.Currencies.CountAsync(),
            ["employees.Employees"] = await employeeDb.Employees.CountAsync(),
            ["crm.Customers"] = await crmDb.Customers.CountAsync(),
            ["crm.CustomerEmployees"] = await crmDb.CustomerEmployees.CountAsync(),
        };

        // TRUNCATE all data tables in one statement. CASCADE handles FK dependencies.
        // __EFMigrationsHistory tables are NOT included; schema structure is preserved.
        await warpDb.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE
                warp."UserTenantMemberships",
                warp."Users",
                warp."Tenants",
                warp."Currencies",
                employees."Employees",
                crm."CustomerEmployees",
                crm."Customers"
            CASCADE
            """);

        return Results.Ok(new
        {
            Message = "All data cleared. Restart the application to reseed default data.",
            RowsCleared = counts
        });
    }
}
