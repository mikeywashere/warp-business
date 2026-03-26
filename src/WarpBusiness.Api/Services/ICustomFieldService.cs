using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Services;

public interface ICustomFieldService
{
    Task<IReadOnlyList<CustomFieldDefinitionDto>> GetDefinitionsAsync(string entityType, CancellationToken ct = default);
    Task<CustomFieldDefinitionDto?> GetDefinitionAsync(Guid id, CancellationToken ct = default);
    Task<CustomFieldDefinitionDto> CreateDefinitionAsync(CreateCustomFieldDefinitionRequest request, CancellationToken ct = default);
    Task<CustomFieldDefinitionDto?> UpdateDefinitionAsync(Guid id, UpdateCustomFieldDefinitionRequest request, CancellationToken ct = default);
    Task<bool> DeleteDefinitionAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CustomFieldValueDto>> GetValuesForContactAsync(Guid contactId, CancellationToken ct = default);
    Task UpsertValuesAsync(Guid contactId, IEnumerable<UpsertCustomFieldValueRequest> values, CancellationToken ct = default);
}
