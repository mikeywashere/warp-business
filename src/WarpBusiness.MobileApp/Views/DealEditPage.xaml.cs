namespace WarpBusiness.MobileApp.Views;

public partial class DealEditPage : ContentPage
{
    public DealEditPage(ViewModels.DealEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
