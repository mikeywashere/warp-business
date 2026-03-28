using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public interface IEmployeePayRateService
{
    Task<List<EmployeePayRateDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<EmployeePayRateDto?> GetCurrentRateAsync(Guid employeeId, CancellationToken ct = default);
    Task<EmployeePayRateDto> CreateAsync(CreateEmployeePayRateRequest request, string userId, CancellationToken ct = default);
    Task<EmployeePayRateDto?> UpdateAsync(Guid id, UpdateEmployeePayRateRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
