namespace WarpBusiness.Shared.Plugins;

public record EmployeeDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    string? Department,
    string? JobTitle,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    bool IsActive,
    Guid? ManagerId,
    string? ManagerName,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record EmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Department,
    string? JobTitle,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    bool IsActive,
    Guid? ManagerId,
    string? Notes
);

public record EmployeePagedResult(
    List<EmployeeDto> Items,
    int Total,
    int Page,
    int PageSize
);
