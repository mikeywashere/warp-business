namespace WarpBusiness.MobileApp.Views;

public partial class TimeEntryDetailPage : ContentPage
{
    public TimeEntryDetailPage(ViewModels.TimeEntryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
