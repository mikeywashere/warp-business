using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.Plugin.TimeTracking.Services;

public interface ICustomerBillingRateService
{
    Task<List<CustomerBillingRateDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<List<CustomerBillingRateDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<CustomerBillingRateDto?> GetCurrentRateAsync(Guid employeeId, Guid companyId, CancellationToken ct = default);
    Task<CustomerBillingRateDto> CreateAsync(CreateCustomerBillingRateRequest request, string userId, CancellationToken ct = default);
    Task<CustomerBillingRateDto?> UpdateAsync(Guid id, UpdateCustomerBillingRateRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
