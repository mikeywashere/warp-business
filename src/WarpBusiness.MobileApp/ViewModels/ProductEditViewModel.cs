using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(ProductId), "id")]
public partial class ProductEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _productId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _shortDescription;
    [ObservableProperty] private string? _slug;
    [ObservableProperty] private string? _sku;
    [ObservableProperty] private string? _barcode;
    [ObservableProperty] private string? _brand;
    [ObservableProperty] private string? _manufacturer;
    [ObservableProperty] private Guid? _categoryId;
    [ObservableProperty] private string _productType = "General";
    [ObservableProperty] private string _status = "Draft";
    [ObservableProperty] private decimal _basePrice;
    [ObservableProperty] private decimal? _compareAtPrice;
    [ObservableProperty] private decimal? _costPrice;
    [ObservableProperty] private string _currency = "USD";
    [ObservableProperty] private decimal? _weight;
    [ObservableProperty] private string? _weightUnit;
    [ObservableProperty] private decimal? _length;
    [ObservableProperty] private decimal? _width;
    [ObservableProperty] private decimal? _height;
    [ObservableProperty] private string? _dimensionUnit;
    [ObservableProperty] private bool _isTaxable = true;
    [ObservableProperty] private string? _taxCode;
    [ObservableProperty] private string? _metaTitle;
    [ObservableProperty] private string? _metaDescription;
    [ObservableProperty] private string? _tags;
    [ObservableProperty] private List<CategoryDto> _categories = [];
    [ObservableProperty] private CategoryDto? _selectedCategory;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public static List<string> ProductTypes => ["General", "Food", "Beverage", "Service"];
    public static List<string> StatusOptions => ["Draft", "Active", "Archived"];

    public ProductEditViewModel(ApiClient api) => _api = api;

    partial void OnProductIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var cats = await _api.GetAllCategoriesAsync();
            if (cats != null) Categories = cats;

            if (!IsNew && Guid.TryParse(ProductId, out var id))
            {
                var p = await _api.GetProductAsync(id);
                if (p != null)
                {
                    Name = p.Name; Description = p.Description; ShortDescription = p.ShortDescription;
                    Slug = p.Slug; Sku = p.Sku; Barcode = p.Barcode; Brand = p.Brand;
                    Manufacturer = p.Manufacturer; CategoryId = p.CategoryId;
                    ProductType = p.ProductType; Status = p.Status; BasePrice = p.BasePrice;
                    CompareAtPrice = p.CompareAtPrice; CostPrice = p.CostPrice; Currency = p.Currency;
                    Weight = p.Weight; WeightUnit = p.WeightUnit;
                    Length = p.Length; Width = p.Width; Height = p.Height;
                    DimensionUnit = p.DimensionUnit; IsTaxable = p.IsTaxable; TaxCode = p.TaxCode;
                    MetaTitle = p.MetaTitle; MetaDescription = p.MetaDescription; Tags = p.Tags;
                    SelectedCategory = Categories.FirstOrDefault(c => c.Id == p.CategoryId);
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Name is required"; return; }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedCategoryId = SelectedCategory?.Id ?? CategoryId;

            if (IsNew)
            {
                await _api.CreateProductAsync(new CreateProductRequest(
                    Name!, Description, ShortDescription, Slug, Sku, Barcode, Brand, Manufacturer,
                    selectedCategoryId, ProductType, Status, BasePrice, CompareAtPrice, CostPrice, Currency,
                    Weight, WeightUnit, Length, Width, Height, DimensionUnit,
                    IsTaxable, TaxCode, MetaTitle, MetaDescription, Tags));
            }
            else if (Guid.TryParse(ProductId, out var id))
            {
                await _api.UpdateProductAsync(id, new UpdateProductRequest(
                    Name!, Description, ShortDescription, Slug, Sku, Barcode, Brand, Manufacturer,
                    selectedCategoryId, ProductType, Status, BasePrice, CompareAtPrice, CostPrice, Currency,
                    Weight, WeightUnit, Length, Width, Height, DimensionUnit,
                    IsTaxable, TaxCode, MetaTitle, MetaDescription, Tags));
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
