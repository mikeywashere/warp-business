using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class ActivityListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<ActivityDto> _activities = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private Guid? _contactId;
    [ObservableProperty] private Guid? _companyId;
    [ObservableProperty] private Guid? _dealId;
    [ObservableProperty] private bool? _isCompleted;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public ActivityListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _api.GetActivitiesAsync(CurrentPage, 20, ContactId, CompanyId, DealId, IsCompleted);
            if (result != null)
            {
                Activities = new ObservableCollection<ActivityDto>(result.Items);
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
    [RelayCommand] private async Task GoToDetailAsync(ActivityDto a) => await Shell.Current.GoToAsync($"activityDetail?id={a.Id}");
    [RelayCommand] private async Task GoToCreateAsync() => await Shell.Current.GoToAsync("activityEdit");
}
