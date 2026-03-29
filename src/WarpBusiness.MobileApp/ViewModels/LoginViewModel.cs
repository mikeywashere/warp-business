using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _password;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _showTenantPicker;
    [ObservableProperty] private MyTenantDto? _selectedTenant;
    [ObservableProperty] private List<MyTenantDto> _tenants = [];

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password)) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _authService.LoginAsync(Email, Password);
            if (result != null)
            {
                var tenants = await _authService.GetMyTenantsAsync();
                var tenantList = tenants?.ToList() ?? [];
                if (tenantList.Count > 1)
                {
                    Tenants = tenantList;
                    ShowTenantPicker = true;
                }
                else
                {
                    if (tenantList.Count == 1)
                        await _authService.SelectTenantAsync(tenantList[0].Id);
                    await Shell.Current.GoToAsync("//dashboard");
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message.Contains("401") ? "Invalid email or password" : $"Login failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SelectTenantAsync()
    {
        if (SelectedTenant == null) return;
        IsBusy = true;
        try
        {
            await _authService.SelectTenantAsync(SelectedTenant.Id);
            await Shell.Current.GoToAsync("//dashboard");
        }
        catch (Exception ex) { ErrorMessage = $"Failed to select tenant: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
        => await Shell.Current.GoToAsync("register");
}
