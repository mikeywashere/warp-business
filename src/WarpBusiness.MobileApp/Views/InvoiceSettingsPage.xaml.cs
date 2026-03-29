namespace WarpBusiness.MobileApp.Views;

public partial class InvoiceSettingsPage : ContentPage
{
    public InvoiceSettingsPage(ViewModels.InvoiceSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.InvoiceSettingsViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
