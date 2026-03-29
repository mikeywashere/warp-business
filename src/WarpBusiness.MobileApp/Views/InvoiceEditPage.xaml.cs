namespace WarpBusiness.MobileApp.Views;

public partial class InvoiceEditPage : ContentPage
{
    public InvoiceEditPage(ViewModels.InvoiceEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
