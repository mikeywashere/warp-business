using WarpBusiness.MobileApp.Services;

namespace WarpBusiness.MobileApp;

public partial class App : Application
{
	public App(AuthService authService)
	{
		InitializeComponent();
		_ = authService.InitializeAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}