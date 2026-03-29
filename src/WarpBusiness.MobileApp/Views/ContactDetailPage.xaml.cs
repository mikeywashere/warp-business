namespace WarpBusiness.MobileApp.Views;

public partial class ContactDetailPage : ContentPage
{
    public ContactDetailPage(ViewModels.ContactDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
