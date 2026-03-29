using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.MobileApp.ViewModels;

[QueryProperty(nameof(CategoryId), "id")]
public partial class CategoryEditViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string? _categoryId;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _slug;
    [ObservableProperty] private Guid? _parentCategoryId;
    [ObservableProperty] private string? _imageUrl;
    [ObservableProperty] private int _displayOrder;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private List<CategoryDto> _parentCategories = [];
    [ObservableProperty] private CategoryDto? _selectedParentCategory;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public CategoryEditViewModel(ApiClient api) => _api = api;

    partial void OnCategoryIdChanged(string? value) { if (Guid.TryParse(value, out _)) { IsNew = false; LoadCommand.Execute(null); } }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var cats = await _api.GetAllCategoriesAsync();
            if (cats != null) ParentCategories = cats;

            if (!IsNew && Guid.TryParse(CategoryId, out var id))
            {
                var c = await _api.GetCategoryAsync(id);
                if (c != null)
                {
                    Name = c.Name; Description = c.Description; Slug = c.Slug;
                    ParentCategoryId = c.ParentCategoryId; ImageUrl = c.ImageUrl;
                    DisplayOrder = c.DisplayOrder; IsActive = c.IsActive;
                    SelectedParentCategory = ParentCategories.FirstOrDefault(x => x.Id == c.ParentCategoryId);
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
            var parentId = SelectedParentCategory?.Id ?? ParentCategoryId;

            if (IsNew)
            {
                await _api.CreateCategoryAsync(new CreateCategoryRequest(
                    Name!, Description, Slug, parentId, ImageUrl, DisplayOrder, IsActive));
            }
            else if (Guid.TryParse(CategoryId, out var id))
            {
                await _api.UpdateCategoryAsync(id, new UpdateCategoryRequest(
                    Name!, Description, Slug, parentId, ImageUrl, DisplayOrder, IsActive));
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
