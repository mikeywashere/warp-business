using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.TimeTracking.Data;
using WarpBusiness.Plugin.TimeTracking.Domain;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public class CustomerBillingRateService : ICustomerBillingRateService
{
    private readonly TimeTrackingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public CustomerBillingRateService(TimeTrackingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<List<CustomerBillingRateDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        return await _context.CustomerBillingRates
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId)
            .OrderBy(r => r.CompanyName)
            .ThenByDescending(r => r.EffectiveDate)
            .Select(r => new CustomerBillingRateDto(
                r.Id,
                r.EmployeeId,
                r.EmployeeName,
                r.CompanyId,
                r.CompanyName,
                r.HourlyRate,
                r.Currency,
                r.EffectiveDate,
                r.EndDate,
                r.Notes,
                r.CreatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<List<CustomerBillingRateDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _context.CustomerBillingRates
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.EmployeeName)
            .ThenByDescending(r => r.EffectiveDate)
            .Select(r => new CustomerBillingRateDto(
                r.Id,
                r.EmployeeId,
                r.EmployeeName,
                r.CompanyId,
                r.CompanyName,
                r.HourlyRate,
                r.Currency,
                r.EffectiveDate,
                r.EndDate,
                r.Notes,
                r.CreatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<CustomerBillingRateDto?> GetCurrentRateAsync(Guid employeeId, Guid companyId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _context.CustomerBillingRates
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.CompanyId == companyId && (r.EndDate == null || r.EndDate >= today))
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => new CustomerBillingRateDto(
                r.Id,
                r.EmployeeId,
                r.EmployeeName,
                r.CompanyId,
                r.CompanyName,
                r.HourlyRate,
                r.Currency,
                r.EffectiveDate,
                r.EndDate,
                r.Notes,
                r.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CustomerBillingRateDto> CreateAsync(CreateCustomerBillingRateRequest request, string userId, CancellationToken ct = default)
    {
        var entity = new CustomerBillingRate
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            CompanyId = request.CompanyId,
            CompanyName = request.CompanyName,
            HourlyRate = request.HourlyRate,
            Currency = request.Currency,
            EffectiveDate = request.EffectiveDate,
            EndDate = request.EndDate,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        _context.CustomerBillingRates.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new CustomerBillingRateDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.CompanyId,
            entity.CompanyName,
            entity.HourlyRate,
            entity.Currency,
            entity.EffectiveDate,
            entity.EndDate,
            entity.Notes,
            entity.CreatedAt
        );
    }

    public async Task<CustomerBillingRateDto?> UpdateAsync(Guid id, UpdateCustomerBillingRateRequest request, CancellationToken ct = default)
    {
        var entity = await _context.CustomerBillingRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return null;

        entity.HourlyRate = request.HourlyRate;
        entity.Currency = request.Currency;
        entity.EffectiveDate = request.EffectiveDate;
        entity.EndDate = request.EndDate;
        entity.Notes = request.Notes;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new CustomerBillingRateDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.CompanyId,
            entity.CompanyName,
            entity.HourlyRate,
            entity.Currency,
            entity.EffectiveDate,
            entity.EndDate,
            entity.Notes,
            entity.CreatedAt
        );
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.CustomerBillingRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return false;

        _context.CustomerBillingRates.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
