namespace WarpBusiness.Plugin.TimeTracking.Domain;

public class EmployeePayRate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public RateType RateType { get; set; } = RateType.Hourly;
    public string Currency { get; set; } = "USD";
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public enum RateType
{
    Hourly,
    Daily,
    Monthly,
    Yearly
}
