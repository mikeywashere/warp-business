using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Services;

public enum DeleteCompanyResult { Deleted, NotFound, HasContacts }

public interface ICompanyService
{
    Task<PagedResult<CompanyDto>> GetCompaniesAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<CompanyDto?> GetCompanyByIdAsync(Guid id, CancellationToken ct = default);
    Task<CompanyDto> CreateCompanyAsync(CreateCompanyRequest request, string userId, CancellationToken ct = default);
    Task<CompanyDto?> UpdateCompanyAsync(Guid id, UpdateCompanyRequest request, CancellationToken ct = default);
    Task<DeleteCompanyResult> DeleteCompanyAsync(Guid id, CancellationToken ct = default);
}
