namespace WarpBusiness.Shared.Crm;

public record CompanyDto(
    Guid Id,
    string Name,
    string? Website,
    string? Industry,
    int? EmployeeCount,
    string? Phone,
    string? Email,
    int ContactCount,
    DateTimeOffset CreatedAt);

public record ContactSummaryDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Title);

public record CompanyDetailDto(
    Guid Id,
    string Name,
    string? Industry,
    string? Website,
    string? Phone,
    string? Email,
    int? EmployeeCount,
    DateTimeOffset CreatedAt,
    int ContactCount,
    IReadOnlyList<ContactSummaryDto> Contacts);

public record CreateCompanyRequest(
    string Name,
    string? Website,
    string? Industry,
    int? EmployeeCount,
    string? Phone,
    string? Email);

public record UpdateCompanyRequest(
    string Name,
    string? Website,
    string? Industry,
    int? EmployeeCount,
    string? Phone,
    string? Email);