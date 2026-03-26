using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.Crm.Domain;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Services;

public class CompanyService : ICompanyService
{
    private readonly CrmDbContext _db;

    public CompanyService(CrmDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<CompanyDto>> GetCompaniesAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _db.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CompanyDto(
                c.Id, c.Name, c.Website, c.Industry, c.EmployeeCount,
                c.Phone, c.Email,
                c.Contacts.Count,
                c.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<CompanyDto>(items, totalCount, page, pageSize);
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Companies
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CompanyDto(
                c.Id, c.Name, c.Website, c.Industry, c.EmployeeCount,
                c.Phone, c.Email,
                c.Contacts.Count,
                c.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequest request, string userId, CancellationToken ct = default)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Website = request.Website,
            Industry = request.Industry,
            EmployeeCount = request.EmployeeCount,
            Phone = request.Phone,
            Email = request.Email,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);

        return (await GetCompanyByIdAsync(company.Id, ct))!;
    }

    public async Task<CompanyDto?> UpdateCompanyAsync(Guid id, UpdateCompanyRequest request, CancellationToken ct = default)
    {
        var company = await _db.Companies.FindAsync([id], ct);
        if (company is null) return null;

        company.Name = request.Name;
        company.Website = request.Website;
        company.Industry = request.Industry;
        company.EmployeeCount = request.EmployeeCount;
        company.Phone = request.Phone;
        company.Email = request.Email;
        company.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetCompanyByIdAsync(id, ct);
    }

    public async Task<DeleteCompanyResult> DeleteCompanyAsync(Guid id, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (company is null) return DeleteCompanyResult.NotFound;
        if (company.Contacts.Any()) return DeleteCompanyResult.HasContacts;

        _db.Companies.Remove(company);
        await _db.SaveChangesAsync(ct);
        return DeleteCompanyResult.Deleted;
    }
}
