namespace WarpBusiness.MobileApp.Views;

public partial class DealDetailPage : ContentPage
{
    public DealDetailPage(ViewModels.DealDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
