using System.Text.Json.Serialization;

namespace WarpBusiness.Scheduling.Models;

/// <summary>State-specific break rule parameters. Seeded at startup; extensible to any US state.</summary>
public class BreakRule
{
    public Guid Id { get; set; }
    /// <summary>US state code (e.g. "WA", "CA", "OR").</summary>
    public string State { get; set; } = string.Empty;
    public BreakRuleType RuleType { get; set; }
    /// <summary>Minimum shift length (minutes) for this rule to trigger.</summary>
    public int MinShiftMinutesToTrigger { get; set; }
    /// <summary>Minimum break duration in minutes.</summary>
    public int BreakDurationMinutes { get; set; }
    public bool IsPaid { get; set; }
    /// <summary>Rest: 1 break required per this many minutes worked (e.g. 240 = per 4 hours).</summary>
    public int? FrequencyMinutes { get; set; }
    /// <summary>Maximum consecutive minutes an employee can work without a rest break (e.g. WA: 180).</summary>
    public int? MaxConsecutiveMinutesWithoutBreak { get; set; }
    /// <summary>Meal: break must start no earlier than this many minutes into the shift (e.g. WA: 120 = 2nd hour).</summary>
    public int? MustStartAfterShiftMinutes { get; set; }
    /// <summary>Meal: break must start no later than this many minutes into the shift (e.g. WA: 300 = 5th hour).</summary>
    public int? MustStartBeforeShiftMinutes { get; set; }
    public bool IsWaivable { get; set; }
    /// <summary>Whether this break counts toward overtime and paid leave calculations.</summary>
    public bool CountsAsHoursWorked { get; set; }
    /// <summary>WA Meal: second meal break required if shift exceeds this many minutes (e.g. 660 = 11 hours).</summary>
    public int? AdditionalBreakAfterMinutes { get; set; }
    /// <summary>WA: extra meal break required if employee works more than this many minutes past scheduled shift end.</summary>
    public int? OvertimeExtraBreakAfterMinutes { get; set; }
    public string? Notes { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BreakRuleType
{
    Rest,
    Meal
}
