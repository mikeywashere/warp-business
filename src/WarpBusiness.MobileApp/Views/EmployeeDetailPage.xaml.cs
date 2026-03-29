namespace WarpBusiness.MobileApp.Views;

public partial class EmployeeDetailPage : ContentPage
{
    public EmployeeDetailPage(ViewModels.EmployeeDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
