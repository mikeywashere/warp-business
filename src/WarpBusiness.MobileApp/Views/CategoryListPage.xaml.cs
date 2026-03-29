namespace WarpBusiness.MobileApp.Views;

public partial class CategoryListPage : ContentPage
{
    public CategoryListPage(ViewModels.CategoryListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.CategoryListViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
