using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Services;

public interface IActivityService
{
    Task<PagedResult<ActivityDto>> GetActivitiesAsync(int page, int pageSize,
        Guid? contactId = null, Guid? companyId = null, Guid? dealId = null,
        bool? isCompleted = null, CancellationToken ct = default);
    Task<ActivityDto?> GetActivityAsync(Guid id, CancellationToken ct = default);
    Task<ActivityDto> CreateActivityAsync(CreateActivityRequest request, string userId, CancellationToken ct = default);
    Task<ActivityDto?> UpdateActivityAsync(Guid id, UpdateActivityRequest request, CancellationToken ct = default);
    Task<bool> CompleteActivityAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteActivityAsync(Guid id, CancellationToken ct = default);
}
