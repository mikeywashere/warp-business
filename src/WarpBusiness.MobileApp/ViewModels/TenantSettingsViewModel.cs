using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class TenantSettingsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;

    [ObservableProperty] private TenantDetailDto? _tenant;
    [ObservableProperty] private ObservableCollection<TenantMemberDto> _members = [];
    [ObservableProperty] private string? _tenantName;
    [ObservableProperty] private string? _displayName;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public TenantSettingsViewModel(ApiClient api, AuthService auth)
    {
        _api = api;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_auth.SelectedTenantId == null) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Tenant = await _api.GetTenantAsync(_auth.SelectedTenantId.Value);
            if (Tenant != null)
            {
                TenantName = Tenant.Name;
                DisplayName = Tenant.DisplayName;
                Members = new ObservableCollection<TenantMemberDto>(Tenant.Members ?? []);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_auth.SelectedTenantId == null || string.IsNullOrWhiteSpace(TenantName)) return;
        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            await _api.UpdateTenantAsync(_auth.SelectedTenantId.Value,
                new UpdateTenantRequest(TenantName!, DisplayName));
            SuccessMessage = "Tenant settings saved";
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveMemberAsync(TenantMemberDto member)
    {
        if (_auth.SelectedTenantId == null) return;
        if (!await Shell.Current.DisplayAlertAsync("Remove", $"Remove {member.FullName ?? member.Email}?", "Yes", "No")) return;
        try
        {
            await _api.RemoveTenantMemberAsync(_auth.SelectedTenantId.Value, member.UserId);
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task ChangeMemberRoleAsync(TenantMemberDto member)
    {
        if (_auth.SelectedTenantId == null) return;
        string role = await Shell.Current.DisplayActionSheetAsync(
            $"Change role for {member.FullName ?? member.Email}", "Cancel", null,
            "Owner", "Admin", "Member");
        if (string.IsNullOrEmpty(role) || role == "Cancel") return;
        try
        {
            await _api.ChangeMemberRoleAsync(_auth.SelectedTenantId.Value, member.UserId,
                new { Role = role });
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
