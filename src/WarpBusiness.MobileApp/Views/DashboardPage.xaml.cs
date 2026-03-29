namespace WarpBusiness.MobileApp.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(ViewModels.DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.DashboardViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
