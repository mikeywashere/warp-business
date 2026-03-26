namespace WarpBusiness.Shared.Crm;

public record CustomFieldDefinitionDto(
    Guid Id,
    string Name,
    string EntityType,
    string FieldType,
    string[]? SelectOptions,
    bool IsRequired,
    int DisplayOrder,
    bool IsActive
);

public record CreateCustomFieldDefinitionRequest(
    string Name,
    string EntityType,
    string FieldType,
    string[]? SelectOptions,
    bool IsRequired,
    int DisplayOrder
);

public record UpdateCustomFieldDefinitionRequest(
    string Name,
    string FieldType,
    string[]? SelectOptions,
    bool IsRequired,
    int DisplayOrder,
    bool IsActive
);

public record CustomFieldValueDto(
    Guid FieldDefinitionId,
    string FieldName,
    string FieldType,
    string[]? SelectOptions,
    bool IsRequired,
    string? Value
);

public record UpsertCustomFieldValueRequest(Guid FieldDefinitionId, string? Value);
