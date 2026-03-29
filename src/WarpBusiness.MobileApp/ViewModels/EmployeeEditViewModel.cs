using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Plugins;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(EmployeeId), "id")]
public partial class EmployeeEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _employeeId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string? _firstName;
    [ObservableProperty] private string? _lastName;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _department;
    [ObservableProperty] private string? _jobTitle;
    [ObservableProperty] private DateTime _hireDate = DateTime.Today;
    [ObservableProperty] private DateTime? _terminationDate;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private Guid? _managerId;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private List<EmployeeDto> _managers = [];

    public EmployeeEditViewModel(ApiClient api) => _api = api;

    partial void OnEmployeeIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var mgrs = await _api.GetManagersAsync();
            if (mgrs != null) Managers = mgrs;

            if (!IsNew && Guid.TryParse(EmployeeId, out var id))
            {
                var emp = await _api.GetEmployeeAsync(id);
                if (emp != null)
                {
                    FirstName = emp.FirstName; LastName = emp.LastName; Email = emp.Email;
                    Phone = emp.Phone; Department = emp.Department; JobTitle = emp.JobTitle;
                    HireDate = emp.HireDate.ToDateTime(TimeOnly.MinValue);
                    TerminationDate = emp.TerminationDate?.ToDateTime(TimeOnly.MinValue);
                    IsActive = emp.IsActive; ManagerId = emp.ManagerId; Notes = emp.Notes;
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(Email))
        { ErrorMessage = "First name, last name, and email are required"; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var request = new EmployeeRequest(
                FirstName!, LastName!, Email!, Phone, Department, JobTitle,
                DateOnly.FromDateTime(HireDate),
                TerminationDate.HasValue ? DateOnly.FromDateTime(TerminationDate.Value) : null,
                IsActive, ManagerId, Notes);

            if (IsNew)
                await _api.CreateEmployeeAsync(request);
            else if (Guid.TryParse(EmployeeId, out var id))
                await _api.UpdateEmployeeAsync(id, request);

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
