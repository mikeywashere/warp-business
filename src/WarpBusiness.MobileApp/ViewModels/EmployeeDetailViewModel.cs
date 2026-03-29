using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Plugins;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(EmployeeId), "id")]
public partial class EmployeeDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _employeeId;
    [ObservableProperty] private EmployeeDto? _employee;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public EmployeeDetailViewModel(ApiClient api) => _api = api;

    partial void OnEmployeeIdChanged(string? value)
    {
        if (Guid.TryParse(value, out _)) LoadCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(EmployeeId, out var id)) return;
        IsBusy = true;
        try { Employee = await _api.GetEmployeeAsync(id); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EditAsync()
        => await Shell.Current.GoToAsync($"employeeEdit?id={EmployeeId}");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(EmployeeId, out var id)) return;
        bool confirm = await Shell.Current.DisplayAlertAsync("Delete", "Delete this employee?", "Yes", "No");
        if (!confirm) return;
        try
        {
            await _api.DeleteEmployeeAsync(id);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        if (!Guid.TryParse(EmployeeId, out var id)) return;
        try
        {
            await _api.DeactivateEmployeeAsync(id);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
