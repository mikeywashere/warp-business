namespace WarpBusiness.MobileApp.Views;

public partial class ProductDetailPage : ContentPage
{
    public ProductDetailPage(ViewModels.ProductDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
