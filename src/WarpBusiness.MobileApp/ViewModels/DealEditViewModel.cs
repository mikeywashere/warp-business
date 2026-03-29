using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(DealId), "id")]
public partial class DealEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _dealId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string? _title;
    [ObservableProperty] private decimal _value;
    [ObservableProperty] private string _currency = "USD";
    [ObservableProperty] private string _stage = "Lead";
    [ObservableProperty] private int _probability = 50;
    [ObservableProperty] private DateTime? _expectedCloseDate;
    [ObservableProperty] private Guid? _contactId;
    [ObservableProperty] private Guid? _companyId;
    [ObservableProperty] private ObservableCollection<CompanyDto> _companies = [];
    [ObservableProperty] private ObservableCollection<ContactDto> _contacts = [];
    [ObservableProperty] private CompanyDto? _selectedCompany;
    [ObservableProperty] private ContactDto? _selectedContact;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public DealEditViewModel(ApiClient api) => _api = api;

    partial void OnDealIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var companiesTask = _api.GetCompaniesAsync(1, 100);
            var contactsTask = _api.GetContactsAsync(1, 100);
            await Task.WhenAll(companiesTask, contactsTask);

            if (companiesTask.Result != null) Companies = new ObservableCollection<CompanyDto>(companiesTask.Result.Items);
            if (contactsTask.Result != null) Contacts = new ObservableCollection<ContactDto>(contactsTask.Result.Items);

            if (!IsNew && Guid.TryParse(DealId, out var id))
            {
                var d = await _api.GetDealAsync(id);
                if (d != null)
                {
                    Title = d.Title; Value = d.Value; Currency = d.Currency;
                    Stage = d.Stage; Probability = d.Probability;
                    ExpectedCloseDate = d.ExpectedCloseDate?.LocalDateTime;
                    ContactId = d.ContactId; CompanyId = d.CompanyId;
                    SelectedCompany = Companies.FirstOrDefault(x => x.Id == d.CompanyId);
                    SelectedContact = Contacts.FirstOrDefault(x => x.Id == d.ContactId);
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) { ErrorMessage = "Title is required"; return; }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedCompanyId = SelectedCompany?.Id ?? CompanyId;
            var selectedContactId = SelectedContact?.Id ?? ContactId;
            DateTimeOffset? closeDate = ExpectedCloseDate.HasValue ? new DateTimeOffset(ExpectedCloseDate.Value) : null;

            if (IsNew)
            {
                await _api.CreateDealAsync(new CreateDealRequest(
                    Title!, Value, Currency, Stage, Probability, closeDate, selectedContactId, selectedCompanyId));
            }
            else if (Guid.TryParse(DealId, out var id))
            {
                await _api.UpdateDealAsync(id, new UpdateDealRequest(
                    Title!, Value, Currency, Stage, Probability, closeDate, selectedContactId, selectedCompanyId));
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
