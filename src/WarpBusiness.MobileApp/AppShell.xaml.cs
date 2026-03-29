using WarpBusiness.MobileApp.Views;

namespace WarpBusiness.MobileApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register detail/edit routes (pushed onto navigation stack)
		Routing.RegisterRoute("register", typeof(RegisterPage));
		Routing.RegisterRoute("companyDetail", typeof(CompanyDetailPage));
		Routing.RegisterRoute("companyEdit", typeof(CompanyEditPage));
		Routing.RegisterRoute("contactDetail", typeof(ContactDetailPage));
		Routing.RegisterRoute("contactEdit", typeof(ContactEditPage));
		Routing.RegisterRoute("dealDetail", typeof(DealDetailPage));
		Routing.RegisterRoute("dealEdit", typeof(DealEditPage));
		Routing.RegisterRoute("activityDetail", typeof(ActivityDetailPage));
		Routing.RegisterRoute("activityEdit", typeof(ActivityEditPage));
		Routing.RegisterRoute("employeeDetail", typeof(EmployeeDetailPage));
		Routing.RegisterRoute("employeeEdit", typeof(EmployeeEditPage));
		Routing.RegisterRoute("productDetail", typeof(ProductDetailPage));
		Routing.RegisterRoute("productEdit", typeof(ProductEditPage));
		Routing.RegisterRoute("categoryEdit", typeof(CategoryEditPage));
		Routing.RegisterRoute("invoiceDetail", typeof(InvoiceDetailPage));
		Routing.RegisterRoute("invoiceEdit", typeof(InvoiceEditPage));
		Routing.RegisterRoute("timeEntryDetail", typeof(TimeEntryDetailPage));
		Routing.RegisterRoute("timeEntryEdit", typeof(TimeEntryEditPage));
	}
}
