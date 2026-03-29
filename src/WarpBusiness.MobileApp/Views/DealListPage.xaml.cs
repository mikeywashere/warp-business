namespace WarpBusiness.MobileApp.Views;

public partial class DealListPage : ContentPage
{
    public DealListPage(ViewModels.DealListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.DealListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
