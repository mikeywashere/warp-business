namespace WarpBusiness.MobileApp.Views;

public partial class AdminUsersPage : ContentPage
{
    public AdminUsersPage(ViewModels.AdminUsersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.AdminUsersViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
