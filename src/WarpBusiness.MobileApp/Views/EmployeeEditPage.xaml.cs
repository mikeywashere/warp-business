namespace WarpBusiness.MobileApp.Views;

public partial class EmployeeEditPage : ContentPage
{
    public EmployeeEditPage(ViewModels.EmployeeEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
