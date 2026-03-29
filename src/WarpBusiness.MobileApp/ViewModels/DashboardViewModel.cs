using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarpBusiness.MobileApp.Services;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.MobileApp.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _welcomeMessage;
    [ObservableProperty] private int _employeeCount;
    [ObservableProperty] private int _companyCount;
    [ObservableProperty] private int _contactCount;
    [ObservableProperty] private int _dealCount;
    [ObservableProperty] private decimal _totalPipelineValue;
    [ObservableProperty] private int _invoiceCount;
    [ObservableProperty] private decimal _totalOutstanding;
    [ObservableProperty] private ObservableCollection<ActivityDto> _recentActivities = [];

    public DashboardViewModel(ApiClient api, AuthService auth)
    {
        _api = api;
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            WelcomeMessage = $"Welcome, {_auth.FullName ?? "User"}!";

            var employeesTask = _api.GetEmployeesAsync(page: 1, pageSize: 1);
            var companiesTask = _api.GetCompaniesAsync(page: 1, pageSize: 1);
            var contactsTask = _api.GetContactsAsync(page: 1, pageSize: 1);
            var dealSummaryTask = _api.GetDealSummaryAsync();
            var invoiceSummaryTask = _api.GetInvoiceSummaryAsync();
            var activitiesTask = _api.GetActivitiesAsync(page: 1, pageSize: 5);

            await Task.WhenAll(employeesTask, companiesTask, contactsTask, dealSummaryTask, invoiceSummaryTask, activitiesTask);

            EmployeeCount = employeesTask.Result?.Total ?? 0;
            CompanyCount = companiesTask.Result?.TotalCount ?? 0;
            ContactCount = contactsTask.Result?.TotalCount ?? 0;

            var dealSummary = dealSummaryTask.Result;
            DealCount = dealSummary?.TotalDealCount ?? 0;
            TotalPipelineValue = dealSummary?.TotalPipelineValue ?? 0;

            var invoiceSummary = invoiceSummaryTask.Result;
            InvoiceCount = invoiceSummary?.TotalInvoices ?? 0;
            TotalOutstanding = invoiceSummary?.TotalOutstanding ?? 0;

            RecentActivities = new ObservableCollection<ActivityDto>(activitiesTask.Result?.Items ?? []);
        }
        catch (Exception ex) { ErrorMessage = $"Failed to load dashboard: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
