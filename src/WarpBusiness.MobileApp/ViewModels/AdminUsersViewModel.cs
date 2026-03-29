using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class AdminUsersViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<UserSummaryDto> _users = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public AdminUsersViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var users = await _api.GetUsersAsync();
            Users = new ObservableCollection<UserSummaryDto>(users ?? []);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleRoleAsync(UserSummaryDto user)
    {
        string action = await Shell.Current.DisplayActionSheetAsync(
            $"Manage roles for {user.FullName}", "Cancel", null,
            "Add Admin", "Remove Admin", "Add Manager", "Remove Manager");
        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        try
        {
            var (role, add) = action switch
            {
                "Add Admin" => ("Admin", true),
                "Remove Admin" => ("Admin", false),
                "Add Manager" => ("Manager", true),
                "Remove Manager" => ("Manager", false),
                _ => (string.Empty, false)
            };
            if (!string.IsNullOrEmpty(role))
            {
                await _api.SetUserRoleAsync(user.Id, role, add);
                await LoadAsync();
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteUserAsync(UserSummaryDto user)
    {
        if (!await Shell.Current.DisplayAlertAsync("Delete", $"Delete user {user.FullName}?", "Yes", "No")) return;
        try { await _api.DeleteUserAsync(user.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateUserFromEmployeeAsync()
    {
        string employeeIdStr = await Shell.Current.DisplayPromptAsync(
            "Create User", "Enter Employee ID:");
        if (!Guid.TryParse(employeeIdStr, out var employeeId)) return;

        IsBusy = true;
        try
        {
            var result = await _api.CreateUserFromEmployeeAsync(employeeId);
            if (result != null)
            {
                await Shell.Current.DisplayAlertAsync("User Created",
                    $"User {result.FullName} created.\nTemp password: {result.TemporaryPassword}", "OK");
                await LoadAsync();
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
