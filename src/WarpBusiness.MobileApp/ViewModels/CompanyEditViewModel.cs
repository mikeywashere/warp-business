using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(CompanyId), "id")]
public partial class CompanyEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _companyId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private string? _website;
    [ObservableProperty] private string? _industry;
    [ObservableProperty] private int? _employeeCount;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public CompanyEditViewModel(ApiClient api) => _api = api;

    partial void OnCompanyIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(CompanyId, out var id)) return;
        IsBusy = true;
        try
        {
            var c = await _api.GetCompanyAsync(id);
            if (c != null)
            {
                Name = c.Name; Website = c.Website; Industry = c.Industry;
                EmployeeCount = c.EmployeeCount; Phone = c.Phone; Email = c.Email;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Name is required"; return; }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsNew)
                await _api.CreateCompanyAsync(new CreateCompanyRequest(Name!, Website, Industry, EmployeeCount, Phone, Email));
            else if (Guid.TryParse(CompanyId, out var id))
                await _api.UpdateCompanyAsync(id, new UpdateCompanyRequest(Name!, Website, Industry, EmployeeCount, Phone, Email));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
