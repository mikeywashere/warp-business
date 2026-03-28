using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.TimeTracking.Data;
using WarpBusiness.Plugin.TimeTracking.Domain;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public class TimeEntryService : ITimeEntryService
{
    private readonly TimeTrackingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public TimeEntryService(TimeTrackingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResult<TimeEntryDto>> GetEntriesAsync(
        int page,
        int pageSize,
        Guid? employeeId,
        Guid? companyId,
        string? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default)
    {
        var query = _context.TimeEntries
            .Include(t => t.TimeEntryType)
            .AsNoTracking();

        if (employeeId.HasValue)
            query = query.Where(t => t.EmployeeId == employeeId);

        if (companyId.HasValue)
            query = query.Where(t => t.CompanyId == companyId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status.ToString() == status);

        if (fromDate.HasValue)
            query = query.Where(t => t.Date >= fromDate);

        if (toDate.HasValue)
            query = query.Where(t => t.Date <= toDate);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TimeEntryDto(
                t.Id,
                t.EmployeeId,
                t.EmployeeName,
                t.Date,
                t.StartTime,
                t.EndTime,
                t.Hours,
                t.TimeEntryTypeId,
                t.TimeEntryType!.Name,
                t.IsBillable,
                t.CompanyId,
                t.CompanyName,
                t.BillingRate,
                t.Description,
                t.Status.ToString(),
                t.CreatedAt
            ))
            .ToListAsync(ct);

        return new PagedResult<TimeEntryDto>(items, totalCount, page, pageSize);
    }

    public async Task<TimeEntryDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TimeEntries
            .Include(t => t.TimeEntryType)
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TimeEntryDetailDto(
                t.Id,
                t.EmployeeId,
                t.EmployeeName,
                t.Date,
                t.StartTime,
                t.EndTime,
                t.Hours,
                t.TimeEntryTypeId,
                t.TimeEntryType!.Name,
                t.IsBillable,
                t.CompanyId,
                t.CompanyName,
                t.BillingRate,
                t.Description,
                t.Status.ToString(),
                t.ApprovedById,
                t.ApprovedAt,
                t.RejectionReason,
                t.CreatedAt,
                t.UpdatedAt,
                t.CreatedBy
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TimeEntryDto> CreateAsync(CreateTimeEntryRequest request, string userId, CancellationToken ct = default)
    {
        var entity = new TimeEntry
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Hours = request.Hours,
            TimeEntryTypeId = request.TimeEntryTypeId,
            IsBillable = request.IsBillable,
            CompanyId = request.CompanyId,
            CompanyName = request.CompanyName,
            BillingRate = request.BillingRate,
            Description = request.Description,
            Status = TimeEntryStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        _context.TimeEntries.Add(entity);
        await _context.SaveChangesAsync(ct);

        var type = await _context.TimeEntryTypes.FindAsync([entity.TimeEntryTypeId], ct);

        return new TimeEntryDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            entity.Hours,
            entity.TimeEntryTypeId,
            type?.Name ?? string.Empty,
            entity.IsBillable,
            entity.CompanyId,
            entity.CompanyName,
            entity.BillingRate,
            entity.Description,
            entity.Status.ToString(),
            entity.CreatedAt
        );
    }

    public async Task<TimeEntryDto?> UpdateAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken ct = default)
    {
        var entity = await _context.TimeEntries
            .Include(t => t.TimeEntryType)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        
        if (entity == null) return null;

        entity.Date = request.Date;
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.Hours = request.Hours;
        entity.TimeEntryTypeId = request.TimeEntryTypeId;
        entity.IsBillable = request.IsBillable;
        entity.CompanyId = request.CompanyId;
        entity.CompanyName = request.CompanyName;
        entity.BillingRate = request.BillingRate;
        entity.Description = request.Description;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new TimeEntryDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            entity.Hours,
            entity.TimeEntryTypeId,
            entity.TimeEntryType?.Name ?? string.Empty,
            entity.IsBillable,
            entity.CompanyId,
            entity.CompanyName,
            entity.BillingRate,
            entity.Description,
            entity.Status.ToString(),
            entity.CreatedAt
        );
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.TimeEntries.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return false;

        _context.TimeEntries.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TimeEntryDto?> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.TimeEntries
            .Include(t => t.TimeEntryType)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        
        if (entity == null || entity.Status != TimeEntryStatus.Draft) return null;

        entity.Status = TimeEntryStatus.Submitted;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new TimeEntryDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            entity.Hours,
            entity.TimeEntryTypeId,
            entity.TimeEntryType?.Name ?? string.Empty,
            entity.IsBillable,
            entity.CompanyId,
            entity.CompanyName,
            entity.BillingRate,
            entity.Description,
            entity.Status.ToString(),
            entity.CreatedAt
        );
    }

    public async Task<TimeEntryDto?> ApproveAsync(Guid id, string approvedById, CancellationToken ct = default)
    {
        var entity = await _context.TimeEntries
            .Include(t => t.TimeEntryType)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        
        if (entity == null || entity.Status != TimeEntryStatus.Submitted) return null;

        entity.Status = TimeEntryStatus.Approved;
        entity.ApprovedById = approvedById;
        entity.ApprovedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new TimeEntryDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            entity.Hours,
            entity.TimeEntryTypeId,
            entity.TimeEntryType?.Name ?? string.Empty,
            entity.IsBillable,
            entity.CompanyId,
            entity.CompanyName,
            entity.BillingRate,
            entity.Description,
            entity.Status.ToString(),
            entity.CreatedAt
        );
    }

    public async Task<TimeEntryDto?> RejectAsync(Guid id, string rejectedById, string reason, CancellationToken ct = default)
    {
        var entity = await _context.TimeEntries
            .Include(t => t.TimeEntryType)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        
        if (entity == null || entity.Status != TimeEntryStatus.Submitted) return null;

        entity.Status = TimeEntryStatus.Rejected;
        entity.ApprovedById = rejectedById;
        entity.ApprovedAt = DateTimeOffset.UtcNow;
        entity.RejectionReason = reason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new TimeEntryDto(
            entity.Id,
            entity.EmployeeId,
            entity.EmployeeName,
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            entity.Hours,
            entity.TimeEntryTypeId,
            entity.TimeEntryType?.Name ?? string.Empty,
            entity.IsBillable,
            entity.CompanyId,
            entity.CompanyName,
            entity.BillingRate,
            entity.Description,
            entity.Status.ToString(),
            entity.CreatedAt
        );
    }
}
