namespace WarpBusiness.Api.Models;

public record TenantRequestResponse(
    Guid Id,
    Guid TenantId,
    string Title,
    string Description,
    string Status,
    string Type,
    string? AssignedToName,
    Guid? AssignedToUserId,
    string? Resolution,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt
);

public record CreateTenantRequestRequest(
    string Title,
    string Description,
    string Type
);

public record UpdateTenantRequestRequest(
    string Title,
    string Description,
    string Status,
    string Type,
    string? AssignedToName,
    Guid? AssignedToUserId,
    string? Resolution
);
