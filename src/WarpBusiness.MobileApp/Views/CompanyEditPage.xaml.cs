namespace WarpBusiness.MobileApp.Views;

public partial class CompanyEditPage : ContentPage
{
    public CompanyEditPage(ViewModels.CompanyEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
