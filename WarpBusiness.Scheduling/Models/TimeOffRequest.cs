using System.Text.Json.Serialization;

namespace WarpBusiness.Scheduling.Models;

public class TimeOffRequest
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOffType Type { get; set; }
    public TimeOffStatus Status { get; set; } = TimeOffStatus.Pending;
    public string? Notes { get; set; }
    public string? ReviewerNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeOffType { Vacation, SickLeave, PersonalLeave, Other }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeOffStatus { Pending, Approved, Denied, Cancelled }
