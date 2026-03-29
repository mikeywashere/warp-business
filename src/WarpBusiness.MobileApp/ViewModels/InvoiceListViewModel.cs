using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class InvoiceListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<InvoiceDto> _invoices = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _selectedStatus;
    [ObservableProperty] private Guid? _companyId;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private InvoiceSummaryDto? _summary;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public static List<string> StatusOptions => ["Draft", "Sent", "Paid", "Overdue", "Cancelled", "Void"];

    public InvoiceListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var from = FromDate.HasValue ? DateOnly.FromDateTime(FromDate.Value) : (DateOnly?)null;
            var to = ToDate.HasValue ? DateOnly.FromDateTime(ToDate.Value) : (DateOnly?)null;

            var invoicesTask = _api.GetInvoicesAsync(CurrentPage, 20, CompanyId, SelectedStatus, from, to);
            var summaryTask = _api.GetInvoiceSummaryAsync();
            await Task.WhenAll(invoicesTask, summaryTask);

            var result = invoicesTask.Result;
            if (result != null)
            {
                Invoices = new ObservableCollection<InvoiceDto>(result.Items);
                TotalPages = result.TotalPages;
                HasNextPage = result.HasNextPage;
                HasPreviousPage = result.HasPreviousPage;
            }
            Summary = summaryTask.Result;
        }
        catch { }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task FilterAsync() { CurrentPage = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextPageAsync() { if (HasNextPage) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] private async Task PreviousPageAsync() { if (HasPreviousPage) { CurrentPage--; await LoadAsync(); } }
    [RelayCommand] private async Task GoToDetailAsync(InvoiceDto inv) => await Shell.Current.GoToAsync($"invoiceDetail?id={inv.Id}");
    [RelayCommand] private async Task GoToCreateAsync() => await Shell.Current.GoToAsync("invoiceEdit");
}
