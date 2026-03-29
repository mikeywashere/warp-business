namespace WarpBusiness.MobileApp.Views;

public partial class ProductListPage : ContentPage
{
    public ProductListPage(ViewModels.ProductListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.ProductListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
