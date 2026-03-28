using System.Collections.Generic;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Services;

public enum DeleteContactEmployeeRelationshipTypeResult
{
    Deleted,
    NotFound,
    InUse
}

public interface IContactEmployeeRelationshipService
{
    Task<IReadOnlyList<ContactEmployeeRelationshipDto>> GetRelationshipsForContactAsync(Guid contactId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ContactEmployeeRelationshipDto>>> GetRelationshipsByContactIdsAsync(IEnumerable<Guid> contactIds, CancellationToken ct = default);
    Task<ContactEmployeeRelationshipDto?> AddRelationshipAsync(Guid contactId, CreateContactEmployeeRelationshipRequest request, string userId, CancellationToken ct = default);
    Task<bool> RemoveRelationshipAsync(Guid contactId, Guid relationshipId, CancellationToken ct = default);
    Task<IReadOnlyList<ContactEmployeeRelationshipTypeDto>> GetRelationshipTypesAsync(CancellationToken ct = default);
    Task<ContactEmployeeRelationshipTypeDto> CreateRelationshipTypeAsync(CreateContactEmployeeRelationshipTypeRequest request, string userId, CancellationToken ct = default);
    Task<ContactEmployeeRelationshipTypeDto?> UpdateRelationshipTypeAsync(Guid id, UpdateContactEmployeeRelationshipTypeRequest request, CancellationToken ct = default);
    Task<DeleteContactEmployeeRelationshipTypeResult> DeleteRelationshipTypeAsync(Guid id, CancellationToken ct = default);
}
