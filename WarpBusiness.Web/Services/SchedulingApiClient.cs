using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarpBusiness.Web.Services;

// ── Enums ──────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScheduleStatus { Draft, Published, InProgress, Completed, Archived }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShiftStatus { Scheduled, Confirmed, InProgress, Completed, NoShow, Absent, Cancelled }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BreakType { Rest, Meal }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BreakRuleType { Rest, Meal }

// ── Work Location DTOs ────────────────────────────────────────────────────────

public record WorkLocationResponse(Guid Id, Guid TenantId, string Name, string? Address, string? City, string State, string? Country, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateWorkLocationRequest(string Name, string? Address, string? City, string State, string? Country);
public record UpdateWorkLocationRequest(string Name, string? Address, string? City, string State, string? Country, bool IsActive);

// ── Position DTOs ─────────────────────────────────────────────────────────────

public record PositionResponse(Guid Id, Guid TenantId, string Name, string? Description, string Color, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public record CreatePositionRequest(string Name, string? Description, string? Color);
public record UpdatePositionRequest(string Name, string? Description, string? Color, bool IsActive);

// ── Schedule Template DTOs ────────────────────────────────────────────────────

public record ScheduleTemplateResponse(Guid Id, Guid TenantId, Guid WorkLocationId, string? WorkLocationName, string Name, string? Description, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<StaffingBlockResponse>? Blocks);
public record StaffingBlockResponse(Guid Id, Guid TemplateId, Guid PositionId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int RequiredCount);
public record CreateScheduleTemplateRequest(string Name, string? Description, Guid WorkLocationId);
public record UpdateScheduleTemplateRequest(string Name, string? Description, bool IsActive);
public record CreateStaffingBlockRequest(Guid PositionId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int RequiredCount);
public record UpdateStaffingBlockRequest(Guid PositionId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int RequiredCount);

// ── Schedule DTOs ─────────────────────────────────────────────────────────────

public record ScheduleResponse(Guid Id, Guid TenantId, Guid WorkLocationId, string? WorkLocationName, string Name, DateOnly StartDate, DateOnly EndDate, ScheduleStatus Status, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateScheduleRequest(string Name, Guid WorkLocationId, DateOnly StartDate, DateOnly EndDate);
public record UpdateScheduleRequest(string Name, DateOnly StartDate, DateOnly EndDate);
public record UpdateScheduleStatusRequest(ScheduleStatus Status);

// ── Shift DTOs ────────────────────────────────────────────────────────────────

public record ShiftResponse(Guid Id, Guid ScheduleId, Guid EmployeeId, Guid PositionId, DateOnly Date,
    TimeOnly ScheduledStartTime, TimeOnly ScheduledEndTime,
    TimeOnly? ActualStartTime, TimeOnly? ActualEndTime,
    ShiftStatus Status, string? Notes, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<BreakResponseDto> Breaks);

public record CreateShiftRequest(Guid EmployeeId, Guid PositionId, DateOnly Date, TimeOnly ScheduledStartTime, TimeOnly ScheduledEndTime, string? Notes);
public record UpdateShiftRequest(Guid EmployeeId, Guid PositionId, TimeOnly ScheduledStartTime, TimeOnly ScheduledEndTime, TimeOnly? ActualStartTime, TimeOnly? ActualEndTime, ShiftStatus Status, string? Notes);

// ── Break DTOs ────────────────────────────────────────────────────────────────

public record BreakResponseDto(Guid Id, Guid ShiftId, BreakType BreakType, bool IsPaid,
    TimeOnly? ScheduledStartTime, TimeOnly? ScheduledEndTime,
    TimeOnly? ActualStartTime, TimeOnly? ActualEndTime,
    bool WasTaken);

public record CreateBreakRequest(BreakType BreakType, bool IsPaid, TimeOnly? ScheduledStartTime, TimeOnly? ScheduledEndTime);
public record UpdateBreakRequest(BreakType BreakType, bool IsPaid, TimeOnly? ScheduledStartTime, TimeOnly? ScheduledEndTime, TimeOnly? ActualStartTime, TimeOnly? ActualEndTime, bool WasTaken);

// ── Break Rule DTOs ───────────────────────────────────────────────────────────

public record BreakRuleResponse(
    Guid Id, string State, BreakRuleType RuleType,
    int MinShiftMinutesToTrigger, int BreakDurationMinutes, bool IsPaid,
    int? FrequencyMinutes, int? MaxConsecutiveMinutesWithoutBreak,
    int? MustStartAfterShiftMinutes, int? MustStartBeforeShiftMinutes,
    bool IsWaivable, bool CountsAsHoursWorked,
    int? AdditionalBreakAfterMinutes, int? OvertimeExtraBreakAfterMinutes,
    string? Notes);

// ── Break Validation ──────────────────────────────────────────────────────────

public record BreakViolation(string RuleDescription, string ViolationType, string Details);

// ── API Client ────────────────────────────────────────────────────────────────

public class SchedulingApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Work Locations
    public Task<List<WorkLocationResponse>?> GetLocationsAsync() =>
        http.GetFromJsonAsync<List<WorkLocationResponse>>("/api/scheduling/locations", JsonOptions);

    public Task<WorkLocationResponse?> GetLocationAsync(Guid id) =>
        http.GetFromJsonAsync<WorkLocationResponse>($"/api/scheduling/locations/{id}", JsonOptions);

    public async Task<WorkLocationResponse?> CreateLocationAsync(CreateWorkLocationRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/scheduling/locations", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkLocationResponse>(JsonOptions);
    }

    public async Task<WorkLocationResponse?> UpdateLocationAsync(Guid id, UpdateWorkLocationRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/scheduling/locations/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkLocationResponse>(JsonOptions);
    }

    public Task DeleteLocationAsync(Guid id) =>
        http.DeleteAsync($"/api/scheduling/locations/{id}");

    // Positions
    public Task<List<PositionResponse>?> GetPositionsAsync() =>
        http.GetFromJsonAsync<List<PositionResponse>>("/api/scheduling/positions", JsonOptions);

    public async Task<PositionResponse?> CreatePositionAsync(CreatePositionRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/scheduling/positions", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PositionResponse>(JsonOptions);
    }

    public async Task<PositionResponse?> UpdatePositionAsync(Guid id, UpdatePositionRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/scheduling/positions/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PositionResponse>(JsonOptions);
    }

    public Task DeletePositionAsync(Guid id) =>
        http.DeleteAsync($"/api/scheduling/positions/{id}");

    // Templates
    public Task<List<ScheduleTemplateResponse>?> GetTemplatesAsync() =>
        http.GetFromJsonAsync<List<ScheduleTemplateResponse>>("/api/scheduling/templates", JsonOptions);

    public Task<ScheduleTemplateResponse?> GetTemplateAsync(Guid id) =>
        http.GetFromJsonAsync<ScheduleTemplateResponse>($"/api/scheduling/templates/{id}", JsonOptions);

    public async Task<ScheduleTemplateResponse?> CreateTemplateAsync(CreateScheduleTemplateRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/scheduling/templates", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);
    }

    public async Task<ScheduleTemplateResponse?> UpdateTemplateAsync(Guid id, UpdateScheduleTemplateRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/scheduling/templates/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);
    }

    public Task DeleteTemplateAsync(Guid id) =>
        http.DeleteAsync($"/api/scheduling/templates/{id}");

    public Task<List<StaffingBlockResponse>?> GetBlocksAsync(Guid templateId) =>
        http.GetFromJsonAsync<List<StaffingBlockResponse>>($"/api/scheduling/templates/{templateId}/blocks", JsonOptions);

    public async Task<StaffingBlockResponse?> AddBlockAsync(Guid templateId, CreateStaffingBlockRequest request)
    {
        var response = await http.PostAsJsonAsync($"/api/scheduling/templates/{templateId}/blocks", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StaffingBlockResponse>(JsonOptions);
    }

    public async Task<StaffingBlockResponse?> UpdateBlockAsync(Guid templateId, Guid blockId, UpdateStaffingBlockRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/scheduling/templates/{templateId}/blocks/{blockId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StaffingBlockResponse>(JsonOptions);
    }

    public Task DeleteBlockAsync(Guid templateId, Guid blockId) =>
        http.DeleteAsync($"/api/scheduling/templates/{templateId}/blocks/{blockId}");

    // Schedules
    public Task<List<ScheduleResponse>?> GetSchedulesAsync() =>
        http.GetFromJsonAsync<List<ScheduleResponse>>("/api/scheduling/schedules", JsonOptions);

    public Task<ScheduleResponse?> GetScheduleAsync(Guid id) =>
        http.GetFromJsonAsync<ScheduleResponse>($"/api/scheduling/schedules/{id}", JsonOptions);

    public async Task<ScheduleResponse?> CreateScheduleAsync(CreateScheduleRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/scheduling/schedules", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleResponse>(JsonOptions);
    }

    public async Task<ScheduleResponse?> UpdateScheduleAsync(Guid id, UpdateScheduleRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/scheduling/schedules/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleResponse>(JsonOptions);
    }

    public async Task<ScheduleResponse?> UpdateScheduleStatusAsync(Guid id, ScheduleStatus status)
    {
        var response = await http.PatchAsJsonAsync($"/api/scheduling/schedules/{id}/status", new UpdateScheduleStatusRequest(status));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleResponse>(JsonOptions);
    }

    public Task DeleteScheduleAsync(Guid id) =>
        http.DeleteAsync($"/api/scheduling/schedules/{id}");

    // Shifts
    public Task<List<ShiftResponse>?> GetShiftsAsync(Guid scheduleId) =>
        http.GetFromJsonAsync<List<ShiftResponse>>($"/api/scheduling/schedules/{scheduleId}/shifts", JsonOptions);

    public async Task<ShiftResponse?> CreateShiftAsync(Guid scheduleId, CreateShiftRequest request)
    {
        var response = await http.PostAsJsonAsync($"/api/scheduling/schedules/{scheduleId}/shifts", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShiftResponse>(JsonOptions);
    }

    public async Task<ShiftResponse?> UpdateShiftAsync(Guid scheduleId, Guid shiftId, UpdateShiftRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/scheduling/schedules/{scheduleId}/shifts/{shiftId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShiftResponse>(JsonOptions);
    }

    public Task DeleteShiftAsync(Guid scheduleId, Guid shiftId) =>
        http.DeleteAsync($"/api/scheduling/schedules/{scheduleId}/shifts/{shiftId}");

    public Task<List<BreakViolation>?> ValidateBreaksAsync(Guid scheduleId, Guid shiftId) =>
        http.GetFromJsonAsync<List<BreakViolation>>($"/api/scheduling/schedules/{scheduleId}/shifts/{shiftId}/validate-breaks", JsonOptions);

    // Breaks
    public async Task<BreakResponseDto?> CreateBreakAsync(Guid scheduleId, Guid shiftId, CreateBreakRequest request)
    {
        var response = await http.PostAsJsonAsync($"/api/scheduling/schedules/{scheduleId}/shifts/{shiftId}/breaks", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BreakResponseDto>(JsonOptions);
    }

    public async Task<BreakResponseDto?> UpdateBreakAsync(Guid scheduleId, Guid shiftId, Guid breakId, UpdateBreakRequest request)
    {
        var response = await http.PutAsJsonAsync($"/api/scheduling/schedules/{scheduleId}/shifts/{shiftId}/breaks/{breakId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BreakResponseDto>(JsonOptions);
    }

    public Task DeleteBreakAsync(Guid scheduleId, Guid shiftId, Guid breakId) =>
        http.DeleteAsync($"/api/scheduling/schedules/{scheduleId}/shifts/{shiftId}/breaks/{breakId}");

    // Break Rules
    public Task<List<BreakRuleResponse>?> GetBreakRulesAsync(string state) =>
        http.GetFromJsonAsync<List<BreakRuleResponse>>($"/api/scheduling/break-rules/{state}", JsonOptions);
}
