using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public interface ITimeEntryService
{
    Task<PagedResult<TimeEntryDto>> GetEntriesAsync(int page, int pageSize, Guid? employeeId, Guid? companyId, string? status, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);
    Task<TimeEntryDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TimeEntryDto> CreateAsync(CreateTimeEntryRequest request, string userId, CancellationToken ct = default);
    Task<TimeEntryDto?> UpdateAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<TimeEntryDto?> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<TimeEntryDto?> ApproveAsync(Guid id, string approvedById, CancellationToken ct = default);
    Task<TimeEntryDto?> RejectAsync(Guid id, string rejectedById, string reason, CancellationToken ct = default);
}
