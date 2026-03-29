using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(InvoiceId), "id")]
public partial class InvoiceDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _invoiceId;
    [ObservableProperty] private InvoiceDetailDto? _invoice;
    [ObservableProperty] private ObservableCollection<InvoiceLineItemDto> _lineItems = [];
    [ObservableProperty] private ObservableCollection<InvoicePaymentDto> _payments = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public InvoiceDetailViewModel(ApiClient api) => _api = api;

    partial void OnInvoiceIdChanged(string? value) { if (Guid.TryParse(value, out _)) LoadCommand.Execute(null); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(InvoiceId, out var id)) return;
        IsBusy = true;
        try
        {
            Invoice = await _api.GetInvoiceAsync(id);
            if (Invoice != null)
            {
                LineItems = new ObservableCollection<InvoiceLineItemDto>(Invoice.LineItems ?? []);
                Payments = new ObservableCollection<InvoicePaymentDto>(Invoice.Payments ?? []);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task EditAsync() => await Shell.Current.GoToAsync($"invoiceEdit?id={InvoiceId}");

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!Guid.TryParse(InvoiceId, out var id)) return;
        try { await _api.SendInvoiceAsync(id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!Guid.TryParse(InvoiceId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Cancel", "Cancel this invoice?", "Yes", "No")) return;
        try { await _api.CancelInvoiceAsync(id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task VoidAsync()
    {
        if (!Guid.TryParse(InvoiceId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Void", "Void this invoice?", "Yes", "No")) return;
        try { await _api.VoidInvoiceAsync(id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(InvoiceId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this invoice?", "Yes", "No")) return;
        try { await _api.DeleteInvoiceAsync(id); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task AddPaymentAsync()
    {
        if (!Guid.TryParse(InvoiceId, out var id)) return;
        string amountStr = await Shell.Current.DisplayPromptAsync("Payment", "Enter amount:", keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(amountStr, out var amount) || amount <= 0) return;

        string method = await Shell.Current.DisplayActionSheetAsync("Payment Method", "Cancel", null,
            "Cash", "Check", "Credit Card", "Bank Transfer", "Other");
        if (string.IsNullOrEmpty(method) || method == "Cancel") return;

        try
        {
            await _api.AddPaymentAsync(id, new CreateInvoicePaymentRequest(
                amount, DateOnly.FromDateTime(DateTime.Today), method));
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(InvoicePaymentDto payment)
    {
        if (!Guid.TryParse(InvoiceId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this payment?", "Yes", "No")) return;
        try { await _api.DeletePaymentAsync(id, payment.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
