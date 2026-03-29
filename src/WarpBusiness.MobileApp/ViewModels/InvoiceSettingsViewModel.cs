using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class InvoiceSettingsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string _prefix = "INV-";
    [ObservableProperty] private int _numberPadding = 5;
    [ObservableProperty] private string _defaultPaymentTerms = "Net 30";
    [ObservableProperty] private int _defaultDueDays = 30;
    [ObservableProperty] private decimal? _defaultTaxRate;
    [ObservableProperty] private string _defaultCurrency = "USD";
    [ObservableProperty] private string? _defaultFooterText;
    [ObservableProperty] private string? _defaultCustomerNotes;
    [ObservableProperty] private string? _companyName;
    [ObservableProperty] private string? _companyAddress;
    [ObservableProperty] private string? _companyPhone;
    [ObservableProperty] private string? _companyEmail;
    [ObservableProperty] private string? _companyLogoUrl;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    public InvoiceSettingsViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _api.GetInvoiceSettingsAsync();
            if (settings != null)
            {
                Prefix = settings.Prefix; NumberPadding = settings.NumberPadding;
                DefaultPaymentTerms = settings.DefaultPaymentTerms;
                DefaultDueDays = settings.DefaultDueDays;
                DefaultTaxRate = settings.DefaultTaxRate; DefaultCurrency = settings.DefaultCurrency;
                DefaultFooterText = settings.DefaultFooterText;
                DefaultCustomerNotes = settings.DefaultCustomerNotes;
                CompanyName = settings.CompanyName; CompanyAddress = settings.CompanyAddress;
                CompanyPhone = settings.CompanyPhone; CompanyEmail = settings.CompanyEmail;
                CompanyLogoUrl = settings.CompanyLogoUrl;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            await _api.UpdateInvoiceSettingsAsync(new UpdateInvoiceSettingsRequest(
                Prefix, NumberPadding, DefaultPaymentTerms, DefaultDueDays,
                DefaultTaxRate, DefaultCurrency, DefaultFooterText, DefaultCustomerNotes,
                CompanyName, CompanyAddress, CompanyPhone, CompanyEmail, CompanyLogoUrl));
            SuccessMessage = "Settings saved successfully";
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
