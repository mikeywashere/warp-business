namespace WarpBusiness.Api.Models;

public record CustomerDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Industry,
    string? CompanySize,
    string? Website,
    string? Notes,
    bool IsActive,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CustomerCreateDto(
    string Name,
    string Email,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? Industry = null,
    string? CompanySize = null,
    string? Website = null,
    string? Notes = null,
    string? Currency = null);

public record CustomerUpdateDto(
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? Industry = null,
    string? CompanySize = null,
    string? Website = null,
    string? Notes = null,
    string? Currency = null);

public record CustomerEmployeeDto(
    Guid CustomerId,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeEmail,
    string Relationship,
    decimal? BillingRate,
    string BillingCurrency,
    DateTimeOffset CreatedAt);

public record EmployeeAssignmentDto(
    Guid EmployeeId,
    string Relationship,
    decimal? BillingRate = null,
    string? BillingCurrency = null);
