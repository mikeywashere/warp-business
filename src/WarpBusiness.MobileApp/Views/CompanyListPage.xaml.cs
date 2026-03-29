namespace WarpBusiness.MobileApp.Views;

public partial class CompanyListPage : ContentPage
{
    public CompanyListPage(ViewModels.CompanyListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.CompanyListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
