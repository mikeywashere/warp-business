using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class ProductListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private ObservableCollection<ProductDto> _products = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private Guid? _selectedCategoryId;
    [ObservableProperty] private string? _selectedStatus;
    [ObservableProperty] private List<CategoryDto> _categories = [];
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPreviousPage;

    public static List<string> StatusOptions => ["Draft", "Active", "Archived"];

    public ProductListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _api.GetProductsAsync(CurrentPage, 20, SearchText, SelectedCategoryId, SelectedStatus);
            if (result != null)
            {
                Products = new ObservableCollection<ProductDto>(result.Items);
                TotalPages = result.TotalPages;
                HasNextPage = result.HasNextPage;
                HasPreviousPage = result.HasPreviousPage;
            }
            if (Categories.Count == 0)
            {
                var cats = await _api.GetAllCategoriesAsync();
                if (cats != null) Categories = cats;
            }
        }
        catch { }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task SearchAsync() { CurrentPage = 1; await LoadAsync(); }
    [RelayCommand] private async Task FilterAsync() { CurrentPage = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextPageAsync() { if (HasNextPage) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] private async Task PreviousPageAsync() { if (HasPreviousPage) { CurrentPage--; await LoadAsync(); } }
    [RelayCommand] private async Task GoToDetailAsync(ProductDto p) => await Shell.Current.GoToAsync($"productDetail?id={p.Id}");
    [RelayCommand] private async Task GoToCreateAsync() => await Shell.Current.GoToAsync("productEdit");
}
