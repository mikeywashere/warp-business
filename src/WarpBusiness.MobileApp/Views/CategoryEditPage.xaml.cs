namespace WarpBusiness.MobileApp.Views;

public partial class CategoryEditPage : ContentPage
{
    public CategoryEditPage(ViewModels.CategoryEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
