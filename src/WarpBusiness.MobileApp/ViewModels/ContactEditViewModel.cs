using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(ContactId), "id")]
public partial class ContactEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _contactId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string? _firstName;
    [ObservableProperty] private string? _lastName;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _jobTitle;
    [ObservableProperty] private Guid? _companyId;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private ObservableCollection<CompanyDto> _companies = [];
    [ObservableProperty] private CompanyDto? _selectedCompany;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public ContactEditViewModel(ApiClient api) => _api = api;

    partial void OnContactIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var companiesResult = await _api.GetCompaniesAsync(1, 100);
            if (companiesResult != null)
                Companies = new ObservableCollection<CompanyDto>(companiesResult.Items);

            if (!IsNew && Guid.TryParse(ContactId, out var id))
            {
                var c = await _api.GetContactAsync(id);
                if (c != null)
                {
                    FirstName = c.FirstName; LastName = c.LastName; Email = c.Email;
                    Phone = c.Phone; JobTitle = c.JobTitle; CompanyId = c.CompanyId;
                    Status = c.Status;
                    if (c.CompanyId.HasValue)
                        SelectedCompany = Companies.FirstOrDefault(x => x.Id == c.CompanyId.Value);
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
        { ErrorMessage = "First name and last name are required"; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedCompanyId = SelectedCompany?.Id ?? CompanyId;

            if (IsNew)
            {
                await _api.CreateContactAsync(new CreateContactRequest(
                    FirstName!, LastName!, Email, Phone, JobTitle, selectedCompanyId));
            }
            else if (Guid.TryParse(ContactId, out var id))
            {
                await _api.UpdateContactAsync(id, new UpdateContactRequest(
                    FirstName!, LastName!, Email, Phone, JobTitle, selectedCompanyId, Status ?? "Active"));
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
