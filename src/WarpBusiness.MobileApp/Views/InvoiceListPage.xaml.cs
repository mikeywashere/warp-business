namespace WarpBusiness.MobileApp.Views;

public partial class InvoiceListPage : ContentPage
{
    public InvoiceListPage(ViewModels.InvoiceListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.InvoiceListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
