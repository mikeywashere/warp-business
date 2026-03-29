namespace WarpBusiness.MobileApp.Views;

public partial class ActivityDetailPage : ContentPage
{
    public ActivityDetailPage(ViewModels.ActivityDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
