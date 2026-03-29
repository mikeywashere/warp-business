namespace WarpBusiness.MobileApp.Views;

public partial class TimeEntryListPage : ContentPage
{
    public TimeEntryListPage(ViewModels.TimeEntryListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.TimeEntryListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
