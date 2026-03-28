using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.TimeTracking.Data;
using WarpBusiness.Plugin.TimeTracking.Domain;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public class TimeEntryTypeService : ITimeEntryTypeService
{
    private readonly TimeTrackingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public TimeEntryTypeService(TimeTrackingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<List<TimeEntryTypeDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _context.TimeEntryTypes.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .Select(t => new TimeEntryTypeDto(
                t.Id,
                t.Name,
                t.Description,
                t.DisplayOrder,
                t.IsActive,
                t.IsBillable,
                t.CreatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<TimeEntryTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TimeEntryTypes
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TimeEntryTypeDto(
                t.Id,
                t.Name,
                t.Description,
                t.DisplayOrder,
                t.IsActive,
                t.IsBillable,
                t.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TimeEntryTypeDto> CreateAsync(CreateTimeEntryTypeRequest request, string userId, CancellationToken ct = default)
    {
        var entity = new TimeEntryType
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            IsBillable = request.IsBillable,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        _context.TimeEntryTypes.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new TimeEntryTypeDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.DisplayOrder,
            entity.IsActive,
            entity.IsBillable,
            entity.CreatedAt
        );
    }

    public async Task<TimeEntryTypeDto?> UpdateAsync(Guid id, UpdateTimeEntryTypeRequest request, CancellationToken ct = default)
    {
        var entity = await _context.TimeEntryTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return null;

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.IsBillable = request.IsBillable;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new TimeEntryTypeDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.DisplayOrder,
            entity.IsActive,
            entity.IsBillable,
            entity.CreatedAt
        );
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.TimeEntryTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null) return false;

        _context.TimeEntryTypes.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
