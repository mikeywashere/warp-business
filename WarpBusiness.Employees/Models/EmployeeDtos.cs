namespace WarpBusiness.Employees.Models;

public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    string? Department,
    string? JobTitle,
    Guid? ManagerId,
    EmploymentStatus EmploymentStatus,
    EmploymentType EmploymentType,
    Guid? UserId);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string? Department,
    string? JobTitle,
    Guid? ManagerId,
    EmploymentStatus EmploymentStatus,
    EmploymentType EmploymentType,
    Guid? UserId);

public record EmployeeResponse(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string? Department,
    string? JobTitle,
    Guid? ManagerId,
    EmploymentStatus EmploymentStatus,
    EmploymentType EmploymentType,
    Guid? UserId,
    Guid TenantId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
