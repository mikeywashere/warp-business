using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty] private string? _firstName;
    [ObservableProperty] private string? _lastName;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _password;
    [ObservableProperty] private string? _confirmPassword;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public RegisterViewModel(AuthService authService) => _authService = authService;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName)) return;
        if (Password != ConfirmPassword) { ErrorMessage = "Passwords don't match"; return; }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _authService.RegisterAsync(Email, Password, FirstName, LastName);
            await Shell.Current.GoToAsync("//dashboard");
        }
        catch (Exception ex) { ErrorMessage = $"Registration failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
