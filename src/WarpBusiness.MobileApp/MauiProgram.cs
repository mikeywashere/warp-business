using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.MobileApp.ViewModels;
using WarpBusiness.MobileApp.Views;

namespace WarpBusiness.MobileApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Auth HttpClient (no auth handler to avoid circular dependency)
		builder.Services.AddHttpClient("AuthClient", client =>
		{
			client.BaseAddress = new Uri(Helpers.Constants.DefaultApiBaseUrl);
		});

		// Auth service (singleton — shared auth state)
		builder.Services.AddSingleton(sp =>
		{
			var factory = sp.GetRequiredService<IHttpClientFactory>();
			var client = factory.CreateClient("AuthClient");
			return new AuthService(client);
		});
		builder.Services.AddTransient<AuthDelegatingHandler>();

		// HttpClient with auth handler for API calls
		builder.Services.AddHttpClient<ApiClient>(client =>
		{
			client.BaseAddress = new Uri(Helpers.Constants.DefaultApiBaseUrl);
		}).AddHttpMessageHandler<AuthDelegatingHandler>();

		// ViewModels
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<RegisterViewModel>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<CompanyListViewModel>();
		builder.Services.AddTransient<CompanyDetailViewModel>();
		builder.Services.AddTransient<CompanyEditViewModel>();
		builder.Services.AddTransient<ContactListViewModel>();
		builder.Services.AddTransient<ContactDetailViewModel>();
		builder.Services.AddTransient<ContactEditViewModel>();
		builder.Services.AddTransient<DealListViewModel>();
		builder.Services.AddTransient<DealDetailViewModel>();
		builder.Services.AddTransient<DealEditViewModel>();
		builder.Services.AddTransient<ActivityListViewModel>();
		builder.Services.AddTransient<ActivityDetailViewModel>();
		builder.Services.AddTransient<ActivityEditViewModel>();
		builder.Services.AddTransient<EmployeeListViewModel>();
		builder.Services.AddTransient<EmployeeDetailViewModel>();
		builder.Services.AddTransient<EmployeeEditViewModel>();
		builder.Services.AddTransient<ProductListViewModel>();
		builder.Services.AddTransient<ProductDetailViewModel>();
		builder.Services.AddTransient<ProductEditViewModel>();
		builder.Services.AddTransient<CategoryListViewModel>();
		builder.Services.AddTransient<CategoryEditViewModel>();
		builder.Services.AddTransient<InvoiceListViewModel>();
		builder.Services.AddTransient<InvoiceDetailViewModel>();
		builder.Services.AddTransient<InvoiceEditViewModel>();
		builder.Services.AddTransient<InvoiceSettingsViewModel>();
		builder.Services.AddTransient<TimeEntryListViewModel>();
		builder.Services.AddTransient<TimeEntryDetailViewModel>();
		builder.Services.AddTransient<TimeEntryEditViewModel>();
		builder.Services.AddTransient<TimeEntryTypesViewModel>();
		builder.Services.AddTransient<AdminUsersViewModel>();
		builder.Services.AddTransient<TenantSettingsViewModel>();

		// Pages
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<RegisterPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<CompanyListPage>();
		builder.Services.AddTransient<CompanyDetailPage>();
		builder.Services.AddTransient<CompanyEditPage>();
		builder.Services.AddTransient<ContactListPage>();
		builder.Services.AddTransient<ContactDetailPage>();
		builder.Services.AddTransient<ContactEditPage>();
		builder.Services.AddTransient<DealListPage>();
		builder.Services.AddTransient<DealDetailPage>();
		builder.Services.AddTransient<DealEditPage>();
		builder.Services.AddTransient<ActivityListPage>();
		builder.Services.AddTransient<ActivityDetailPage>();
		builder.Services.AddTransient<ActivityEditPage>();
		builder.Services.AddTransient<EmployeeListPage>();
		builder.Services.AddTransient<EmployeeDetailPage>();
		builder.Services.AddTransient<EmployeeEditPage>();
		builder.Services.AddTransient<ProductListPage>();
		builder.Services.AddTransient<ProductDetailPage>();
		builder.Services.AddTransient<ProductEditPage>();
		builder.Services.AddTransient<CategoryListPage>();
		builder.Services.AddTransient<CategoryEditPage>();
		builder.Services.AddTransient<InvoiceListPage>();
		builder.Services.AddTransient<InvoiceDetailPage>();
		builder.Services.AddTransient<InvoiceEditPage>();
		builder.Services.AddTransient<InvoiceSettingsPage>();
		builder.Services.AddTransient<TimeEntryListPage>();
		builder.Services.AddTransient<TimeEntryDetailPage>();
		builder.Services.AddTransient<TimeEntryEditPage>();
		builder.Services.AddTransient<TimeEntryTypesPage>();
		builder.Services.AddTransient<AdminUsersPage>();
		builder.Services.AddTransient<TenantSettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
