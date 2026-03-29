namespace WarpBusiness.MobileApp.Views;

public partial class EmployeeListPage : ContentPage
{
    public EmployeeListPage(ViewModels.EmployeeListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.EmployeeListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
