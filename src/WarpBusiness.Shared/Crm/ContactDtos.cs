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
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? JobTitle,
    Guid? CompanyId,
    IReadOnlyList<UpsertCustomFieldValueRequest>? CustomFields = null);

public record UpdateContactRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? JobTitle,
    Guid? CompanyId,
    string Status,
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