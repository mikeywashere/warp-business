namespace WarpBusiness.Shared.TimeTracking;

public record TimeEntryTypeDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    bool IsBillable,
    DateTimeOffset CreatedAt
);

public record CreateTimeEntryTypeRequest(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    bool IsBillable
);

public record UpdateTimeEntryTypeRequest(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    bool IsBillable
);
