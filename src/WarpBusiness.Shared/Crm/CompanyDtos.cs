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
