namespace WarpBusiness.MobileApp.Views;

public partial class ContactListPage : ContentPage
{
    public ContactListPage(ViewModels.ContactListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.ContactListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
