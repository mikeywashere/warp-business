namespace WarpBusiness.Shared.Crm;

public record ActivityDto(
    Guid Id,
    string Type,
    string Subject,
    string? Description,
    DateTimeOffset DueDate,
    DateTimeOffset? CompletedAt,
    bool IsCompleted,
    Guid? ContactId,
    string? ContactName,
    Guid? CompanyId,
    string? CompanyName,
    Guid? DealId,
    string? DealTitle,
    DateTimeOffset CreatedAt
);

public record CreateActivityRequest(
    string Type,
    string Subject,
    string? Description,
    DateTimeOffset DueDate,
    Guid? ContactId,
    Guid? DealId
);

public record UpdateActivityRequest(
    string Type,
    string Subject,
    string? Description,
    DateTimeOffset DueDate,
    bool IsCompleted
);
