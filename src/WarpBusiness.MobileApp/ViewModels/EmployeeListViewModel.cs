using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Plugins;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class EmployeeListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<EmployeeDto> _employees = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private string? _selectedDepartment;
    [ObservableProperty] private List<string> _departments = [];
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public EmployeeListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _api.GetEmployeesAsync(CurrentPage, 20, false, SelectedDepartment, SearchText);
            if (result != null)
            {
                Employees = new ObservableCollection<EmployeeDto>(result.Items);
                TotalPages = (int)Math.Ceiling((double)result.Total / result.PageSize);
                HasNextPage = CurrentPage < TotalPages;
                HasPreviousPage = CurrentPage > 1;
            }
            if (Departments.Count == 0)
            {
                var depts = await _api.GetDepartmentsAsync();
                if (depts != null) Departments = depts;
            }
        }
        catch { }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (HasNextPage) { CurrentPage++; await LoadAsync(); }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (HasPreviousPage) { CurrentPage--; await LoadAsync(); }
    }

    [RelayCommand]
    private async Task GoToDetailAsync(EmployeeDto employee)
        => await Shell.Current.GoToAsync($"employeeDetail?id={employee.Id}");

    [RelayCommand]
    private async Task GoToCreateAsync()
        => await Shell.Current.GoToAsync("employeeEdit");
}
