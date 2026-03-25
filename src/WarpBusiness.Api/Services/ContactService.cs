using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Domain;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Services;

public class ContactService : IContactService
{
    private readonly ApplicationDbContext _db;

    public ContactService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ContactDto>> GetContactsAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _db.Contacts
            .Include(c => c.Company)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(term) ||
                c.LastName.ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ContactDto(
                c.Id, c.FirstName, c.LastName, c.FullName,
                c.Email, c.Phone, c.JobTitle,
                c.CompanyId, c.Company != null ? c.Company.Name : null,
                c.Status.ToString(), c.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ContactDto>(items, totalCount, page, pageSize);
    }

    public async Task<ContactDto?> GetContactByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Contacts
            .Include(c => c.Company)
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ContactDto(
                c.Id, c.FirstName, c.LastName, c.FullName,
                c.Email, c.Phone, c.JobTitle,
                c.CompanyId, c.Company != null ? c.Company.Name : null,
                c.Status.ToString(), c.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ContactDto> CreateContactAsync(CreateContactRequest request, string userId, CancellationToken ct = default)
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            JobTitle = request.JobTitle,
            CompanyId = request.CompanyId,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);

        return (await GetContactByIdAsync(contact.Id, ct))!;
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid id, UpdateContactRequest request, CancellationToken ct = default)
    {
        var contact = await _db.Contacts.FindAsync([id], ct);
        if (contact is null) return null;

        contact.FirstName = request.FirstName;
        contact.LastName = request.LastName;
        contact.Email = request.Email;
        contact.Phone = request.Phone;
        contact.JobTitle = request.JobTitle;
        contact.CompanyId = request.CompanyId;
        contact.Status = Enum.Parse<ContactStatus>(request.Status);
        contact.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetContactByIdAsync(id, ct);
    }

    public async Task<bool> DeleteContactAsync(Guid id, CancellationToken ct = default)
    {
        var contact = await _db.Contacts.FindAsync([id], ct);
        if (contact is null) return false;
        _db.Contacts.Remove(contact);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
