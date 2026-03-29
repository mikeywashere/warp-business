namespace WarpBusiness.MobileApp.Views;

public partial class ActivityEditPage : ContentPage
{
    public ActivityEditPage(ViewModels.ActivityEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
