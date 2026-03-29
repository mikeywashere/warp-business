using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class DealListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<DealDto> _deals = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _selectedStage;
    [ObservableProperty] private DealPipelineSummary? _pipelineSummary;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public DealListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var dealsTask = _api.GetDealsAsync(CurrentPage, 20, SelectedStage);
            var summaryTask = _api.GetDealSummaryAsync();
            await Task.WhenAll(dealsTask, summaryTask);

            var result = dealsTask.Result;
            if (result != null)
            {
                Deals = new ObservableCollection<DealDto>(result.Items);
                TotalPages = result.TotalPages;
                HasNextPage = result.HasNextPage;
                HasPreviousPage = result.HasPreviousPage;
            }
            PipelineSummary = summaryTask.Result;
        }
        catch { }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task FilterAsync() { CurrentPage = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextPageAsync() { if (HasNextPage) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] private async Task PreviousPageAsync() { if (HasPreviousPage) { CurrentPage--; await LoadAsync(); } }
    [RelayCommand] private async Task GoToDetailAsync(DealDto d) => await Shell.Current.GoToAsync($"dealDetail?id={d.Id}");
    [RelayCommand] private async Task GoToCreateAsync() => await Shell.Current.GoToAsync("dealEdit");
}
