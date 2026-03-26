using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Domain;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Services;

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _db;

    public ActivityService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ActivityDto>> GetActivitiesAsync(int page, int pageSize,
        Guid? contactId = null, Guid? companyId = null, Guid? dealId = null,
        bool? isCompleted = null, CancellationToken ct = default)
    {
        var query = _db.Activities
            .Include(a => a.Contact).ThenInclude(c => c!.Company)
            .Include(a => a.Deal).ThenInclude(d => d!.Company)
            .AsNoTracking();

        if (contactId.HasValue)
            query = query.Where(a => a.ContactId == contactId);

        if (companyId.HasValue)
            query = query.Where(a =>
                (a.Contact != null && a.Contact.CompanyId == companyId) ||
                (a.Deal != null && a.Deal.CompanyId == companyId));

        if (dealId.HasValue)
            query = query.Where(a => a.DealId == dealId);

        if (isCompleted.HasValue)
            query = query.Where(a => a.CompletedAt.HasValue == isCompleted.Value);

        var totalCount = await query.CountAsync(ct);
        var raw = await query
            .OrderBy(a => a.ScheduledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = raw.Select(MapToDto).ToList();
        return new PagedResult<ActivityDto>(items, totalCount, page, pageSize);
    }

    public async Task<ActivityDto?> GetActivityAsync(Guid id, CancellationToken ct = default)
    {
        var activity = await _db.Activities
            .Include(a => a.Contact).ThenInclude(c => c!.Company)
            .Include(a => a.Deal).ThenInclude(d => d!.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return activity is null ? null : MapToDto(activity);
    }

    public async Task<ActivityDto> CreateActivityAsync(CreateActivityRequest request, string userId, CancellationToken ct = default)
    {
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Type = Enum.Parse<ActivityType>(request.Type, ignoreCase: true),
            Title = request.Subject,
            Notes = request.Description,
            ScheduledAt = request.DueDate,
            ContactId = request.ContactId,
            DealId = request.DealId,
            OwnerId = userId,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);

        return (await GetActivityAsync(activity.Id, ct))!;
    }

    public async Task<ActivityDto?> UpdateActivityAsync(Guid id, UpdateActivityRequest request, CancellationToken ct = default)
    {
        var activity = await _db.Activities.FindAsync([id], ct);
        if (activity is null) return null;

        activity.Type = Enum.Parse<ActivityType>(request.Type, ignoreCase: true);
        activity.Title = request.Subject;
        activity.Notes = request.Description;
        activity.ScheduledAt = request.DueDate;

        if (request.IsCompleted && !activity.CompletedAt.HasValue)
            activity.CompletedAt = DateTimeOffset.UtcNow;
        else if (!request.IsCompleted)
            activity.CompletedAt = null;

        await _db.SaveChangesAsync(ct);
        return await GetActivityAsync(id, ct);
    }

    public async Task<bool> CompleteActivityAsync(Guid id, CancellationToken ct = default)
    {
        var activity = await _db.Activities.FindAsync([id], ct);
        if (activity is null) return false;

        activity.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteActivityAsync(Guid id, CancellationToken ct = default)
    {
        var activity = await _db.Activities.FindAsync([id], ct);
        if (activity is null) return false;

        _db.Activities.Remove(activity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static ActivityDto MapToDto(Activity a) => new ActivityDto(
        a.Id,
        a.Type.ToString(),
        a.Title,
        a.Notes,
        a.ScheduledAt,
        a.CompletedAt,
        a.CompletedAt.HasValue,
        a.ContactId,
        a.Contact is not null ? a.Contact.FirstName + " " + a.Contact.LastName : null,
        a.Contact?.CompanyId ?? a.Deal?.CompanyId,
        a.Contact?.Company?.Name ?? a.Deal?.Company?.Name,
        a.DealId,
        a.Deal?.Title,
        a.CreatedAt
    );
}
