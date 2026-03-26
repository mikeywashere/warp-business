using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Services;

public interface IContactService
{
    Task<PagedResult<ContactDto>> GetContactsAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<ContactDto?> GetContactByIdAsync(Guid id, CancellationToken ct = default);
    Task<ContactDto?> GetContactByEmailAsync(string email, CancellationToken ct = default);
    Task<ContactDto> CreateContactAsync(CreateContactRequest request, string userId, CancellationToken ct = default);
    Task<ContactDto?> UpdateContactAsync(Guid id, UpdateContactRequest request, CancellationToken ct = default);
    Task<bool> DeleteContactAsync(Guid id, CancellationToken ct = default);
}
