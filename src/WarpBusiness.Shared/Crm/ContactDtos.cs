using System.ComponentModel.DataAnnotations;

namespace WarpBusiness.Shared.Crm;

public record ContactDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string? Email,
    string? Phone,
    string? JobTitle,
    Guid? CompanyId,
    string? CompanyName,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CustomFieldValueDto> CustomFields);

public record CreateContactRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [Phone, MaxLength(50)] string? Phone,
    [MaxLength(200)] string? JobTitle,
    Guid? CompanyId,
    IReadOnlyList<UpsertCustomFieldValueRequest>? CustomFields = null);

public record UpdateContactRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [Phone, MaxLength(50)] string? Phone,
    [MaxLength(200)] string? JobTitle,
    Guid? CompanyId,
    [Required] string Status,
    IReadOnlyList<UpsertCustomFieldValueRequest>? CustomFields = null);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}