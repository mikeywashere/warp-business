using WarpBusiness.Employees.Models;

namespace WarpBusiness.Api.Models;

public record CreateEmployeeWithUserRequest(
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
    string Role);

public record UpdateEmployeeWithUserRequest(
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
    string? Role);
