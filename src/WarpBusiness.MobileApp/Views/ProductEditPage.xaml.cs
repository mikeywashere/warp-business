namespace WarpBusiness.MobileApp.Views;

public partial class ProductEditPage : ContentPage
{
    public ProductEditPage(ViewModels.ProductEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
