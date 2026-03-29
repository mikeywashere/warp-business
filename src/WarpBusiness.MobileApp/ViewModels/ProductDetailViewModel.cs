using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(ProductId), "id")]
public partial class ProductDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _productId;
    [ObservableProperty] private ProductDetailDto? _product;
    [ObservableProperty] private ObservableCollection<ProductImageDto> _images = [];
    [ObservableProperty] private ObservableCollection<ProductVariantDto> _variants = [];
    [ObservableProperty] private ObservableCollection<ProductIngredientDto> _ingredients = [];
    [ObservableProperty] private ObservableCollection<ProductOptionDto> _options = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public ProductDetailViewModel(ApiClient api) => _api = api;

    partial void OnProductIdChanged(string? value) { if (Guid.TryParse(value, out _)) LoadCommand.Execute(null); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(ProductId, out var id)) return;
        IsBusy = true;
        try
        {
            Product = await _api.GetProductAsync(id);
            if (Product != null)
            {
                Images = new ObservableCollection<ProductImageDto>(Product.Images ?? []);
                Variants = new ObservableCollection<ProductVariantDto>(Product.Variants ?? []);
                Ingredients = new ObservableCollection<ProductIngredientDto>(Product.Ingredients ?? []);
                Options = new ObservableCollection<ProductOptionDto>(Product.Options ?? []);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task EditAsync() => await Shell.Current.GoToAsync($"productEdit?id={ProductId}");

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!Guid.TryParse(ProductId, out var id)) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete", "Delete this product?", "Yes", "No")) return;
        try { await _api.DeleteProductAsync(id); await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteImageAsync(ProductImageDto image)
    {
        if (!Guid.TryParse(ProductId, out var productId)) return;
        try { await _api.DeleteProductImageAsync(productId, image.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteVariantAsync(ProductVariantDto variant)
    {
        if (!Guid.TryParse(ProductId, out var productId)) return;
        try { await _api.DeleteProductVariantAsync(productId, variant.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteIngredientAsync(ProductIngredientDto ingredient)
    {
        if (!Guid.TryParse(ProductId, out var productId)) return;
        try { await _api.DeleteProductIngredientAsync(productId, ingredient.Id); await LoadAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
