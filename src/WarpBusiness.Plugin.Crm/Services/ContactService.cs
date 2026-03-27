using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.Crm.Domain;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Services;

public class ContactService : IContactService
{
    private readonly CrmDbContext _db;
    private readonly ICustomFieldService _customFields;
    private readonly ITenantContext _tenantContext;

    public ContactService(CrmDbContext db, ICustomFieldService customFields, ITenantContext tenantContext)
    {
        _db = db;
        _customFields = customFields;
        _tenantContext = tenantContext;
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
        var contacts = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Batch-fetch all custom field values for this page
        var contactIds = contacts.Select(c => c.Id).ToHashSet();
        var allValues = await _db.CustomFieldValues
            .Include(v => v.FieldDefinition)
            .AsNoTracking()
            .Where(v => contactIds.Contains(v.ContactId) && v.FieldDefinition.IsActive)
            .ToListAsync(ct);

        var definitions = await _db.CustomFieldDefinitions
            .AsNoTracking()
            .Where(d => d.EntityType == "Contact" && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync(ct);

        var valuesByContact = allValues.GroupBy(v => v.ContactId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(v => v.FieldDefinitionId));

        var items = contacts.Select(c =>
        {
            valuesByContact.TryGetValue(c.Id, out var contactValues);
            var customFields = definitions.Select(d =>
            {
                var val = contactValues is not null && contactValues.TryGetValue(d.Id, out var v) ? v.Value : null;
                return new CustomFieldValueDto(d.Id, d.Name, d.FieldType,
                    DeserializeOptions(d.SelectOptions), d.IsRequired, val);
            }).ToList();

            return new ContactDto(c.Id, c.FirstName, c.LastName, c.FullName,
                c.Email, c.Phone, c.JobTitle,
                c.CompanyId, c.Company?.Name,
                c.Status.ToString(), c.CreatedAt, customFields);
        }).ToList();

        return new PagedResult<ContactDto>(items, totalCount, page, pageSize);
    }

    public async Task<ContactDto?> GetContactByIdAsync(Guid id, CancellationToken ct = default)
    {
        var contact = await _db.Contacts
            .Include(c => c.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (contact is null) return null;

        var customFields = await _customFields.GetValuesForContactAsync(id, ct);
        return MapToDto(contact, customFields);
    }

    public async Task<ContactDto?> GetContactByEmailAsync(string email, CancellationToken ct = default)
    {
        var contact = await _db.Contacts
            .Include(c => c.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == email.ToLower(), ct);

        if (contact is null) return null;

        var customFields = await _customFields.GetValuesForContactAsync(contact.Id, ct);
        return MapToDto(contact, customFields);
    }

    public async Task<ContactDto> CreateContactAsync(CreateContactRequest request, string userId, CancellationToken ct = default)
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email?.ToLowerInvariant(),
            Phone = request.Phone,
            JobTitle = request.JobTitle,
            CompanyId = request.CompanyId,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);

        await _customFields.UpsertValuesAsync(contact.Id, request.CustomFields ?? [], ct);

        return (await GetContactByIdAsync(contact.Id, ct))!;
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid id, UpdateContactRequest request, CancellationToken ct = default)
    {
        var contact = await _db.Contacts.FindAsync([id], ct);
        if (contact is null) return null;

        contact.FirstName = request.FirstName;
        contact.LastName = request.LastName;
        contact.Email = request.Email?.ToLowerInvariant();
        contact.Phone = request.Phone;
        contact.JobTitle = request.JobTitle;
        contact.CompanyId = request.CompanyId;
        contact.Status = Enum.Parse<ContactStatus>(request.Status);
        contact.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _customFields.UpsertValuesAsync(id, request.CustomFields ?? [], ct);

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

    private static ContactDto MapToDto(Contact c, IReadOnlyList<CustomFieldValueDto> customFields) =>
        new(c.Id, c.FirstName, c.LastName, c.FullName,
            c.Email, c.Phone, c.JobTitle,
            c.CompanyId, c.Company?.Name,
            c.Status.ToString(), c.CreatedAt, customFields);

    private static string[]? DeserializeOptions(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json); }
        catch { return null; }
    }
}
