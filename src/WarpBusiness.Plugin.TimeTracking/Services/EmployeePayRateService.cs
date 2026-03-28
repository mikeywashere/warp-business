using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.TimeTracking.Data;
using WarpBusiness.Plugin.TimeTracking.Domain;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public class EmployeePayRateService : IEmployeePayRateService
{
    private readonly TimeTrackingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public EmployeePayRateService(TimeTrackingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<List<EmployeePayRateDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        return await _context.EmployeePayRates
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => new EmployeePayRateDto(
                r.Id,
                r.EmployeeId,
                r.EmployeeName,
                r.Rate,
                r.RateType.ToString(),
                r.Currency,
                r.EffectiveDate,
                r.EndDate,
                r.Notes,
                r.CreatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<EmployeePayRateDto?> GetCurrentRateAsync(Guid employeeId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _context.EmployeePayRates
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && (r.EndDate == null || r.EndDate >= today))
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => new EmployeePayRateDto(
                r.Id,
                r.EmployeeId,
                r.EmployeeName,
                r.Rate,
                r.RateType.ToString(),
                r.Currency,
                r.EffectiveDate,
                r.EndDate,
                r.Notes,
                r.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EmployeePayRateDto> CreateAsync(CreateEmployeePayRateRequest request, string userId, CancellationToken ct = default)
    {
        var entity = new EmployeePayRate
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            Rate = request.Rate,
            RateType = Enum.Parse<RateType>(request.RateType),
            Currency = request.Currency,
            EffectiveDate = request.EffectiveDate,
            EndDate = request.EndDate,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        _context.EmployeePayRates.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new EmployeePayRateDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.Rate,
            entity.RateType.ToString(),
            entity.Currency,
            entity.EffectiveDate,
            entity.EndDate,
            entity.Notes,
            entity.CreatedAt
        );
    }

    public async Task<EmployeePayRateDto?> UpdateAsync(Guid id, UpdateEmployeePayRateRequest request, CancellationToken ct = default)
    {
        var entity = await _context.EmployeePayRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return null;

        entity.Rate = request.Rate;
        entity.RateType = Enum.Parse<RateType>(request.RateType);
        entity.Currency = request.Currency;
        entity.EffectiveDate = request.EffectiveDate;
        entity.EndDate = request.EndDate;
        entity.Notes = request.Notes;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new EmployeePayRateDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.Rate,
            entity.RateType.ToString(),
            entity.Currency,
            entity.EffectiveDate,
            entity.EndDate,
            entity.Notes,
            entity.CreatedAt
        );
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.EmployeePayRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return false;

        _context.EmployeePayRates.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
