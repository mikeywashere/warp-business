namespace WarpBusiness.MobileApp.Views;

public partial class TimeEntryEditPage : ContentPage
{
    public TimeEntryEditPage(ViewModels.TimeEntryEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
