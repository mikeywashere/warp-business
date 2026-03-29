using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Plugins;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(TimeEntryId), "id")]
public partial class TimeEntryEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _timeEntryId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private Guid? _employeeId;
    [ObservableProperty] private string? _employeeName;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private TimeSpan? _startTime;
    [ObservableProperty] private TimeSpan? _endTime;
    [ObservableProperty] private decimal _hours;
    [ObservableProperty] private Guid? _timeEntryTypeId;
    [ObservableProperty] private bool _isBillable;
    [ObservableProperty] private Guid? _companyId;
    [ObservableProperty] private string? _companyName;
    [ObservableProperty] private decimal? _billingRate;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private ObservableCollection<EmployeeDto> _employees = [];
    [ObservableProperty] private ObservableCollection<TimeEntryTypeDto> _entryTypes = [];
    [ObservableProperty] private ObservableCollection<CompanyDto> _companies = [];
    [ObservableProperty] private EmployeeDto? _selectedEmployee;
    [ObservableProperty] private TimeEntryTypeDto? _selectedEntryType;
    [ObservableProperty] private CompanyDto? _selectedCompany;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public TimeEntryEditViewModel(ApiClient api) => _api = api;

    partial void OnTimeEntryIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var employeesTask = _api.GetEmployeesAsync(1, 100);
            var typesTask = _api.GetTimeEntryTypesAsync();
            var companiesTask = _api.GetCompaniesAsync(1, 100);
            await Task.WhenAll(employeesTask, typesTask, companiesTask);

            if (employeesTask.Result != null) Employees = new ObservableCollection<EmployeeDto>(employeesTask.Result.Items);
            if (typesTask.Result != null) EntryTypes = new ObservableCollection<TimeEntryTypeDto>(typesTask.Result);
            if (companiesTask.Result != null) Companies = new ObservableCollection<CompanyDto>(companiesTask.Result.Items);

            if (!IsNew && Guid.TryParse(TimeEntryId, out var id))
            {
                var t = await _api.GetTimeEntryAsync(id);
                if (t != null)
                {
                    EmployeeId = t.EmployeeId; EmployeeName = t.EmployeeName;
                    Date = t.Date.ToDateTime(TimeOnly.MinValue);
                    StartTime = t.StartTime?.ToTimeSpan();
                    EndTime = t.EndTime?.ToTimeSpan();
                    Hours = t.Hours; TimeEntryTypeId = t.TimeEntryTypeId;
                    IsBillable = t.IsBillable; CompanyId = t.CompanyId;
                    CompanyName = t.CompanyName; BillingRate = t.BillingRate;
                    Description = t.Description;
                    SelectedEmployee = Employees.FirstOrDefault(e => e.Id == t.EmployeeId);
                    SelectedEntryType = EntryTypes.FirstOrDefault(et => et.Id == t.TimeEntryTypeId);
                    SelectedCompany = Companies.FirstOrDefault(c => c.Id == t.CompanyId);
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var empId = SelectedEmployee?.Id ?? EmployeeId;
        var typeId = SelectedEntryType?.Id ?? TimeEntryTypeId;
        if (empId == null || typeId == null) { ErrorMessage = "Employee and entry type are required"; return; }
        if (Hours <= 0) { ErrorMessage = "Hours must be greater than 0"; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedCompanyId = SelectedCompany?.Id ?? CompanyId;
            var empName = SelectedEmployee?.FullName ?? EmployeeName ?? "";
            var compName = SelectedCompany?.Name ?? CompanyName;
            var startTimeOnly = StartTime.HasValue ? TimeOnly.FromTimeSpan(StartTime.Value) : (TimeOnly?)null;
            var endTimeOnly = EndTime.HasValue ? TimeOnly.FromTimeSpan(EndTime.Value) : (TimeOnly?)null;

            if (IsNew)
            {
                await _api.CreateTimeEntryAsync(new CreateTimeEntryRequest(
                    empId.Value, empName, DateOnly.FromDateTime(Date),
                    startTimeOnly, endTimeOnly, Hours, typeId.Value,
                    IsBillable, selectedCompanyId, compName, BillingRate, Description));
            }
            else if (Guid.TryParse(TimeEntryId, out var id))
            {
                await _api.UpdateTimeEntryAsync(id, new UpdateTimeEntryRequest(
                    DateOnly.FromDateTime(Date), startTimeOnly, endTimeOnly, Hours,
                    typeId.Value, IsBillable, selectedCompanyId, compName, BillingRate, Description));
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
