namespace WarpBusiness.MobileApp.Views;

public partial class CompanyDetailPage : ContentPage
{
    public CompanyDetailPage(ViewModels.CompanyDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
