using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.Crm.Domain;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Services;

public class ContactEmployeeRelationshipService : IContactEmployeeRelationshipService
{
    private readonly CrmDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ContactEmployeeRelationshipService(CrmDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ContactEmployeeRelationshipDto>> GetRelationshipsForContactAsync(Guid contactId, CancellationToken ct = default)
    {
        var relationships = await _db.ContactEmployeeRelationships
            .Include(r => r.RelationshipType)
            .AsNoTracking()
            .Where(r => r.ContactId == contactId)
            .OrderBy(r => r.RelationshipType.DisplayOrder)
            .ThenBy(r => r.EmployeeName)
            .ToListAsync(ct);

        return relationships.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ContactEmployeeRelationshipDto>>> GetRelationshipsByContactIdsAsync(IEnumerable<Guid> contactIds, CancellationToken ct = default)
    {
        var contactArray = contactIds as Guid[] ?? contactIds.ToArray();
        if (contactArray.Length == 0)
            return new Dictionary<Guid, IReadOnlyList<ContactEmployeeRelationshipDto>>();

        var relationships = await _db.ContactEmployeeRelationships
            .Include(r => r.RelationshipType)
            .AsNoTracking()
            .Where(r => contactArray.Contains(r.ContactId))
            .OrderBy(r => r.RelationshipType.DisplayOrder)
            .ThenBy(r => r.EmployeeName)
            .ToListAsync(ct);

        return relationships
            .GroupBy(r => r.ContactId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ContactEmployeeRelationshipDto>)g.Select(MapToDto).ToList());
    }

    public async Task<ContactEmployeeRelationshipDto?> AddRelationshipAsync(Guid contactId, CreateContactEmployeeRelationshipRequest request, string userId, CancellationToken ct = default)
    {
        var contact = await _db.Contacts.FindAsync(new object[] { contactId }, ct);
        if (contact is null)
            return null;

        var type = await _db.ContactEmployeeRelationshipTypes.FindAsync(new object[] { request.RelationshipTypeId }, ct);
        if (type is null)
            return null;

        var relationship = new ContactEmployeeRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ContactId = contactId,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            EmployeeEmail = request.EmployeeEmail,
            RelationshipTypeId = request.RelationshipTypeId,
            Notes = request.Notes,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.ContactEmployeeRelationships.Add(relationship);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(relationship).Reference(r => r.RelationshipType).LoadAsync(ct);

        return MapToDto(relationship);
    }

    public async Task<bool> RemoveRelationshipAsync(Guid contactId, Guid relationshipId, CancellationToken ct = default)
    {
        var relationship = await _db.ContactEmployeeRelationships
            .FirstOrDefaultAsync(r => r.Id == relationshipId && r.ContactId == contactId, ct);

        if (relationship is null)
            return false;

        _db.ContactEmployeeRelationships.Remove(relationship);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ContactEmployeeRelationshipTypeDto>> GetRelationshipTypesAsync(CancellationToken ct = default)
    {
        var types = await _db.ContactEmployeeRelationshipTypes
            .AsNoTracking()
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return types.Select(MapToDto).ToList();
    }

    public async Task<ContactEmployeeRelationshipTypeDto> CreateRelationshipTypeAsync(CreateContactEmployeeRelationshipTypeRequest request, string userId, CancellationToken ct = default)
    {
        var type = new ContactEmployeeRelationshipType
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.ContactEmployeeRelationshipTypes.Add(type);
        await _db.SaveChangesAsync(ct);

        return MapToDto(type);
    }

    public async Task<ContactEmployeeRelationshipTypeDto?> UpdateRelationshipTypeAsync(Guid id, UpdateContactEmployeeRelationshipTypeRequest request, CancellationToken ct = default)
    {
        var type = await _db.ContactEmployeeRelationshipTypes.FindAsync(new object[] { id }, ct);
        if (type is null)
            return null;

        type.Name = request.Name;
        type.Description = request.Description;
        type.DisplayOrder = request.DisplayOrder;
        type.IsActive = request.IsActive;
        type.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(type);
    }

    public async Task<DeleteContactEmployeeRelationshipTypeResult> DeleteRelationshipTypeAsync(Guid id, CancellationToken ct = default)
    {
        var type = await _db.ContactEmployeeRelationshipTypes.FindAsync(new object[] { id }, ct);
        if (type is null)
            return DeleteContactEmployeeRelationshipTypeResult.NotFound;

        var inUse = await _db.ContactEmployeeRelationships.AnyAsync(r => r.RelationshipTypeId == id, ct);
        if (inUse)
            return DeleteContactEmployeeRelationshipTypeResult.InUse;

        _db.ContactEmployeeRelationshipTypes.Remove(type);
        await _db.SaveChangesAsync(ct);
        return DeleteContactEmployeeRelationshipTypeResult.Deleted;
    }

    private static ContactEmployeeRelationshipDto MapToDto(ContactEmployeeRelationship relationship) =>
        new(
            relationship.Id,
            relationship.ContactId,
            relationship.EmployeeId,
            relationship.EmployeeName,
            relationship.EmployeeEmail,
            relationship.RelationshipTypeId,
            relationship.RelationshipType.Name,
            relationship.Notes,
            relationship.CreatedAt);

    private static ContactEmployeeRelationshipTypeDto MapToDto(ContactEmployeeRelationshipType type) =>
        new(type.Id, type.Name, type.Description, type.DisplayOrder, type.IsActive);
}
