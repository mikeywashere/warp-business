using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class CategoryListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<CategoryDto> _categories = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public CategoryListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _api.GetCategoriesAsync(CurrentPage, 20);
            if (result != null)
            {
                Categories = new ObservableCollection<CategoryDto>(result.Items);
                TotalPages = result.TotalPages;
                HasNextPage = result.HasNextPage;
                HasPreviousPage = result.HasPreviousPage;
            }
        }
        catch { }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task NextPageAsync() { if (HasNextPage) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] private async Task PreviousPageAsync() { if (HasPreviousPage) { CurrentPage--; await LoadAsync(); } }
    [RelayCommand] private async Task GoToEditAsync(CategoryDto c) => await Shell.Current.GoToAsync($"categoryEdit?id={c.Id}");
    [RelayCommand] private async Task GoToCreateAsync() => await Shell.Current.GoToAsync("categoryEdit");

    [RelayCommand]
    private async Task DeleteAsync(CategoryDto category)
    {
        if (!await Shell.Current.DisplayAlertAsync("Delete", $"Delete category '{category.Name}'?", "Yes", "No")) return;
        try { await _api.DeleteCategoryAsync(category.Id); await LoadAsync(); }
        catch { }
    }
}
