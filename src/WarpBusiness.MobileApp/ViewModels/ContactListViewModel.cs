using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class ContactListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<ContactDto> _contacts = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public ContactListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _api.GetContactsAsync(CurrentPage, 20, SearchText);
            if (result != null)
            {
                Contacts = new ObservableCollection<ContactDto>(result.Items);
                TotalPages = result.TotalPages;
                HasNextPage = result.HasNextPage;
                HasPreviousPage = result.HasPreviousPage;
            }
        }
        catch { }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task SearchAsync() { CurrentPage = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextPageAsync() { if (HasNextPage) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] private async Task PreviousPageAsync() { if (HasPreviousPage) { CurrentPage--; await LoadAsync(); } }
    [RelayCommand] private async Task GoToDetailAsync(ContactDto c) => await Shell.Current.GoToAsync($"contactDetail?id={c.Id}");
    [RelayCommand] private async Task GoToCreateAsync() => await Shell.Current.GoToAsync("contactEdit");
}
