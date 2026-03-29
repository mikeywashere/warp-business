namespace WarpBusiness.MobileApp.Views;

public partial class TenantSettingsPage : ContentPage
{
    public TenantSettingsPage(ViewModels.TenantSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.TenantSettingsViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
