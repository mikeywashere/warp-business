using System.Text.Json.Serialization;

namespace WarpBusiness.Employees.Models;

public class Employee
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateOnly HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = [];
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public Guid? UserId { get; set; }
    public Guid TenantId { get; set; }
    public decimal PayAmount { get; set; }
    public PayType PayType { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmploymentStatus
{
    Active,
    OnLeave,
    Terminated,
    Suspended
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmploymentType
{
    FullTime,
    PartTime,
    Contract,
    Intern
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PayType
{
    Hourly,
    Salary
}
