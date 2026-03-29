namespace WarpBusiness.MobileApp.Views;

public partial class TimeEntryTypesPage : ContentPage
{
    public TimeEntryTypesPage(ViewModels.TimeEntryTypesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.TimeEntryTypesViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
