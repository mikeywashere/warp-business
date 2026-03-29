namespace WarpBusiness.MobileApp.Views;

public partial class ActivityListPage : ContentPage
{
    public ActivityListPage(ViewModels.ActivityListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.ActivityListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
