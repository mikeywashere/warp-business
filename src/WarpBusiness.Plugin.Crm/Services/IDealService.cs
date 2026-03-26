using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Services;

public interface IDealService
{
    Task<PagedResult<DealDto>> GetDealsAsync(int page, int pageSize, string? search, string? stage, CancellationToken ct = default);
    Task<DealDto?> GetDealByIdAsync(Guid id, CancellationToken ct = default);
    Task<DealDto> CreateDealAsync(CreateDealRequest request, string userId, CancellationToken ct = default);
    Task<DealDto?> UpdateDealAsync(Guid id, UpdateDealRequest request, CancellationToken ct = default);
    Task<bool> DeleteDealAsync(Guid id, CancellationToken ct = default);
    Task<DealPipelineSummary> GetDealSummaryAsync(CancellationToken ct = default);
}
