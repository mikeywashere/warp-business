using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public interface ITimeEntryTypeService
{
    Task<List<TimeEntryTypeDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<TimeEntryTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TimeEntryTypeDto> CreateAsync(CreateTimeEntryTypeRequest request, string userId, CancellationToken ct = default);
    Task<TimeEntryTypeDto?> UpdateAsync(Guid id, UpdateTimeEntryTypeRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
