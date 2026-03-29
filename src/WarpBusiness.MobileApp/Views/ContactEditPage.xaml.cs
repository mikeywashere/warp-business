namespace WarpBusiness.MobileApp.Views;

public partial class ContactEditPage : ContentPage
{
    public ContactEditPage(ViewModels.ContactEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
