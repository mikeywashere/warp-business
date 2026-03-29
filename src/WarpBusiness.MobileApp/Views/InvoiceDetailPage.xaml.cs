namespace WarpBusiness.MobileApp.Views;

public partial class InvoiceDetailPage : ContentPage
{
    public InvoiceDetailPage(ViewModels.InvoiceDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
