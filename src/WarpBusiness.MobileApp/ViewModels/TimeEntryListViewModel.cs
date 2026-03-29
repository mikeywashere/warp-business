using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class TimeEntryListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<TimeEntryDto> _timeEntries = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private Guid? _employeeId;
    [ObservableProperty] private Guid? _companyId;
    [ObservableProperty] private string? _selectedStatus;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public static List<string> StatusOptions => ["Draft", "Submitted", "Approved", "Rejected"];

    public TimeEntryListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var from = FromDate.HasValue ? DateOnly.FromDateTime(FromDate.Value) : (DateOnly?)null;
            var to = ToDate.HasValue ? DateOnly.FromDateTime(ToDate.Value) : (DateOnly?)null;

            var result = await _api.GetTimeEntriesAsync(CurrentPage, 20, EmployeeId, CompanyId, SelectedStatus, from, to);
            if (result != null)
            {
                TimeEntries = new ObservableCollection<TimeEntryDto>(result.Items);
                TotalPages = result.TotalPages;
                HasNextPage = result.HasNextPage;
                HasPreviousPage = result.HasPreviousPage;
            }
        }
        catch { }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task FilterAsync() { CurrentPage = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextPageAsync() { if (HasNextPage) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] private async Task PreviousPageAsync() { if (HasPreviousPage) { CurrentPage--; await LoadAsync(); } }
    [RelayCommand] private async Task GoToDetailAsync(TimeEntryDto t) => await Shell.Current.GoToAsync($"timeEntryDetail?id={t.Id}");
    [RelayCommand] private async Task GoToCreateAsync() => await Shell.Current.GoToAsync("timeEntryEdit");
}
