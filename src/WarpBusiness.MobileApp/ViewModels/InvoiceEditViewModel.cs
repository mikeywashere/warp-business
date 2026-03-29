using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(InvoiceId), "id")]
public partial class InvoiceEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _invoiceId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private Guid? _companyId;
    [ObservableProperty] private string? _companyName;
    [ObservableProperty] private Guid? _contactId;
    [ObservableProperty] private string? _contactName;
    [ObservableProperty] private string? _billingAddress;
    [ObservableProperty] private string? _shippingAddress;
    [ObservableProperty] private DateTime _issueDate = DateTime.Today;
    [ObservableProperty] private DateTime _dueDate = DateTime.Today.AddDays(30);
    [ObservableProperty] private string? _paymentTerms;
    [ObservableProperty] private string _currency = "USD";
    [ObservableProperty] private decimal? _discountPercent;
    [ObservableProperty] private decimal? _discountFixed;
    [ObservableProperty] private decimal? _taxRate;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _customerNotes;
    [ObservableProperty] private string? _footerText;
    [ObservableProperty] private ObservableCollection<InvoiceLineItemDto> _lineItems = [];
    [ObservableProperty] private ObservableCollection<CompanyDto> _companies = [];
    [ObservableProperty] private ObservableCollection<ContactDto> _contacts = [];
    [ObservableProperty] private CompanyDto? _selectedCompany;
    [ObservableProperty] private ContactDto? _selectedContact;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public InvoiceEditViewModel(ApiClient api) => _api = api;

    partial void OnInvoiceIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

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

            if (!IsNew && Guid.TryParse(InvoiceId, out var id))
            {
                var inv = await _api.GetInvoiceAsync(id);
                if (inv != null)
                {
                    CompanyId = inv.CompanyId; CompanyName = inv.CompanyName;
                    ContactId = inv.ContactId; ContactName = inv.ContactName;
                    BillingAddress = inv.BillingAddress; ShippingAddress = inv.ShippingAddress;
                    IssueDate = inv.IssueDate.ToDateTime(TimeOnly.MinValue);
                    DueDate = inv.DueDate.ToDateTime(TimeOnly.MinValue);
                    PaymentTerms = inv.PaymentTerms; Currency = inv.Currency;
                    DiscountPercent = inv.DiscountPercent; DiscountFixed = inv.DiscountFixed;
                    TaxRate = inv.TaxRate; Notes = inv.Notes;
                    CustomerNotes = inv.CustomerNotes; FooterText = inv.FooterText;
                    LineItems = new ObservableCollection<InvoiceLineItemDto>(inv.LineItems ?? []);
                    SelectedCompany = Companies.FirstOrDefault(c => c.Id == inv.CompanyId);
                    SelectedContact = Contacts.FirstOrDefault(c => c.Id == inv.ContactId);
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddLineItemAsync()
    {
        if (!Guid.TryParse(InvoiceId, out var invoiceId) || IsNew) return;
        try
        {
            await _api.AddLineItemAsync(invoiceId, new CreateInvoiceLineItemRequest(
                Description: "New line item", Quantity: 1, UnitPrice: 0));
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteLineItemAsync(InvoiceLineItemDto lineItem)
    {
        if (!Guid.TryParse(InvoiceId, out var invoiceId)) return;
        try { await _api.DeleteLineItemAsync(invoiceId, lineItem.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedCompanyId = SelectedCompany?.Id ?? CompanyId;
            var selectedContactId = SelectedContact?.Id ?? ContactId;

            if (IsNew)
            {
                await _api.CreateInvoiceAsync(new CreateInvoiceRequest(
                    selectedCompanyId, SelectedCompany?.Name ?? CompanyName,
                    selectedContactId, SelectedContact?.FullName ?? ContactName,
                    BillingAddress, ShippingAddress,
                    DateOnly.FromDateTime(IssueDate), DateOnly.FromDateTime(DueDate),
                    PaymentTerms, Currency, DiscountPercent, DiscountFixed, TaxRate,
                    Notes, CustomerNotes, FooterText));
            }
            else if (Guid.TryParse(InvoiceId, out var id))
            {
                await _api.UpdateInvoiceAsync(id, new UpdateInvoiceRequest(
                    selectedCompanyId, SelectedCompany?.Name ?? CompanyName,
                    selectedContactId, SelectedContact?.FullName ?? ContactName,
                    BillingAddress, ShippingAddress,
                    DateOnly.FromDateTime(IssueDate), DateOnly.FromDateTime(DueDate),
                    PaymentTerms, Currency, DiscountPercent, DiscountFixed, TaxRate,
                    Notes, CustomerNotes, FooterText));
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
