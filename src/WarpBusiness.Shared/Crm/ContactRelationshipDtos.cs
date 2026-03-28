using System.ComponentModel.DataAnnotations;

namespace WarpBusiness.Shared.Crm;

public record ContactEmployeeRelationshipDto(
    Guid Id,
    Guid ContactId,
    Guid EmployeeId,
    string EmployeeName,
    string? EmployeeEmail,
    Guid RelationshipTypeId,
    string RelationshipTypeName,
    string? Notes,
    DateTimeOffset CreatedAt);

public record CreateContactEmployeeRelationshipRequest(
    [Required] Guid EmployeeId,
    [Required, MaxLength(200)] string EmployeeName,
    [EmailAddress, MaxLength(256)] string? EmployeeEmail,
    [Required] Guid RelationshipTypeId,
    [MaxLength(1000)] string? Notes = null);

public record ContactEmployeeRelationshipTypeDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive);

public record CreateContactEmployeeRelationshipTypeRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(500)] string? Description = null,
    int DisplayOrder = 0,
    bool IsActive = true);

public record UpdateContactEmployeeRelationshipTypeRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(500)] string? Description = null,
    int DisplayOrder = 0,
    bool IsActive = true);
