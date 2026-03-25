namespace WarpBusiness.Shared.Crm;

public record DealDto(
    Guid Id,
    string Title,
    decimal Value,
    string Currency,
    string Stage,
    int Probability,
    DateTimeOffset? ExpectedCloseDate,
    Guid? ContactId,
    string? ContactName,
    Guid? CompanyId,
    string? CompanyName,
    string OwnerId,
    DateTimeOffset CreatedAt);

public record CreateDealRequest(
    string Title,
    decimal Value,
    string Currency,
    string Stage,
    int Probability,
    DateTimeOffset? ExpectedCloseDate,
    Guid? ContactId,
    Guid? CompanyId);

public record UpdateDealRequest(
    string Title,
    decimal Value,
    string Currency,
    string Stage,
    int Probability,
    DateTimeOffset? ExpectedCloseDate,
    Guid? ContactId,
    Guid? CompanyId);

public record DealPipelineSummary(
    IReadOnlyList<DealStageSummary> Stages,
    decimal TotalPipelineValue,
    int TotalDealCount);

public record DealStageSummary(
    string Stage,
    int Count,
    decimal TotalValue);
