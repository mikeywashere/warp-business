using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(CompanyId), "id")]
public partial class CompanyDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _companyId;
    [ObservableProperty] private CompanyDetailDto? _company;
    [ObservableProperty] private ObservableCollection<ContactSummaryDto> _contacts = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public CompanyDetailViewModel(ApiClient api) => _api = api;

    partial void OnCompanyIdChanged(string? value) { if (Guid.TryParse(value, out _)) LoadCommand.Execute(null); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(CompanyId, out var id)) return;
        IsBusy = true;
        try
        {
            Company = await _api.GetCompanyAsync(id);
            if (Company?.Contacts != null)
                Contacts = new ObservableCollection<ContactSummaryDto>(Company.Contacts);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task EditAsync() => await Shell.Current.GoToAsync($"companyEdit?id={CompanyId}");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(CompanyId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this company?", "Yes", "No")) return;
        try { await _api.DeleteCompanyAsync(id); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
