using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.TimeTracking;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.TimeTracking;

public class TimeTrackingControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public TimeTrackingControllerTests(WarpTestFactory factory) => _factory = factory;

    private async Task<(HttpClient client, string token)> AuthenticateAsync(string? email = null)
    {
        var client = _factory.CreateClient();
        email ??= $"tt-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        return (client, token);
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = $"tt-admin-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return (client, auth.Token);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Auth Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetEntryTypes_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("api/timetracking/entry-types");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEntries_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("api/timetracking/entries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEntryTypes_ReturnsForbidden_WhenNoTenantToken()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, $"notenant-{Guid.NewGuid()}@example.com");
        client.SetBearerToken(token);

        var response = await client.GetAsync("api/timetracking/entry-types");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteEntryType_ReturnsForbidden_WhenNotAdmin()
    {
        var (client, _) = await AuthenticateAsync();
        var createRequest = new CreateTimeEntryTypeRequest("Regular", "Regular hours", 1, true, true);
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var deleteResponse = await client.DeleteAsync($"api/timetracking/entry-types/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Time Entry Type Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateEntryType_ReturnsCreated_WithCorrectData()
    {
        var (client, _) = await AuthenticateAsync();
        var request = new CreateTimeEntryTypeRequest(
            "Regular Hours",
            "Standard working hours",
            1,
            true,
            true
        );

        var response = await client.PostAsJsonAsync("api/timetracking/entry-types", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var entryType = await response.Content.ReadFromJsonAsync<TimeEntryTypeDto>();
        entryType!.Name.Should().Be("Regular Hours");
        entryType.Description.Should().Be("Standard working hours");
        entryType.DisplayOrder.Should().Be(1);
        entryType.IsActive.Should().BeTrue();
        entryType.IsBillable.Should().BeTrue();
        entryType.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllEntryTypes_ReturnsCreatedTypes()
    {
        var (client, _) = await AuthenticateAsync();
        var request1 = new CreateTimeEntryTypeRequest("Regular", "Regular hours", 1, true, true);
        var request2 = new CreateTimeEntryTypeRequest("Overtime", "Overtime hours", 2, true, true);
        await client.PostAsJsonAsync("api/timetracking/entry-types", request1);
        await client.PostAsJsonAsync("api/timetracking/entry-types", request2);

        var response = await client.GetAsync("api/timetracking/entry-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.Content.ReadFromJsonAsync<List<TimeEntryTypeDto>>();
        types.Should().NotBeEmpty();
        types!.Should().Contain(t => t.Name == "Regular");
        types.Should().Contain(t => t.Name == "Overtime");
    }

    [Fact]
    public async Task UpdateEntryType_ReturnsUpdatedData()
    {
        var (client, _) = await AuthenticateAsync();
        var createRequest = new CreateTimeEntryTypeRequest("Regular", "Regular hours", 1, true, true);
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var updateRequest = new UpdateTimeEntryTypeRequest("Regular Time", "Updated description", 10, true, false);
        var updateResponse = await client.PutAsJsonAsync($"api/timetracking/entry-types/{created!.Id}", updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();
        updated!.Name.Should().Be("Regular Time");
        updated.Description.Should().Be("Updated description");
        updated.DisplayOrder.Should().Be(10);
        updated.IsBillable.Should().BeFalse();
    }

    [Fact]
    public async Task GetEntryTypeById_ReturnsCorrectType()
    {
        var (client, _) = await AuthenticateAsync();
        var createRequest = new CreateTimeEntryTypeRequest("PTO", "Paid time off", 5, true, false);
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var getResponse = await client.GetAsync($"api/timetracking/entry-types/{created!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();
        retrieved!.Id.Should().Be(created.Id);
        retrieved.Name.Should().Be("PTO");
        retrieved.IsBillable.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Pay Rate Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreatePayRate_ReturnsCreated()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var request = new CreateEmployeePayRateRequest(
            employeeId,
            "John Doe",
            75.00m,
            "Hourly",
            "USD",
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            "Initial rate"
        );

        var response = await client.PostAsJsonAsync("api/timetracking/pay-rates", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payRate = await response.Content.ReadFromJsonAsync<EmployeePayRateDto>();
        payRate!.EmployeeId.Should().Be(employeeId);
        payRate.EmployeeName.Should().Be("John Doe");
        payRate.Rate.Should().Be(75.00m);
        payRate.RateType.Should().Be("Hourly");
        payRate.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task GetPayRatesByEmployee_ReturnsCreatedRates()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var request1 = new CreateEmployeePayRateRequest(
            employeeId, "Jane Smith", 70.00m, "Hourly", "USD",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null
        );
        var request2 = new CreateEmployeePayRateRequest(
            employeeId, "Jane Smith", 80.00m, "Hourly", "USD",
            DateOnly.FromDateTime(DateTime.UtcNow), null, "Raise"
        );
        await client.PostAsJsonAsync("api/timetracking/pay-rates", request1);
        await client.PostAsJsonAsync("api/timetracking/pay-rates", request2);

        var response = await client.GetAsync($"api/timetracking/pay-rates/employee/{employeeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rates = await response.Content.ReadFromJsonAsync<List<EmployeePayRateDto>>();
        rates.Should().HaveCount(2);
        rates.Should().Contain(r => r.Rate == 70.00m);
        rates.Should().Contain(r => r.Rate == 80.00m);
    }

    [Fact]
    public async Task GetCurrentPayRate_ReturnsMostRecentActiveRate()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var oldRequest = new CreateEmployeePayRateRequest(
            employeeId, "Bob Jones", 60.00m, "Hourly", "USD",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-31)), null
        );
        var currentRequest = new CreateEmployeePayRateRequest(
            employeeId, "Bob Jones", 85.00m, "Hourly", "USD",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), null, "Current rate"
        );
        await client.PostAsJsonAsync("api/timetracking/pay-rates", oldRequest);
        await client.PostAsJsonAsync("api/timetracking/pay-rates", currentRequest);

        var response = await client.GetAsync($"api/timetracking/pay-rates/employee/{employeeId}/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentRate = await response.Content.ReadFromJsonAsync<EmployeePayRateDto>();
        currentRate!.Rate.Should().Be(85.00m);
        currentRate.Notes.Should().Be("Current rate");
    }

    [Fact]
    public async Task UpdatePayRate_ReturnsUpdatedData()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var createRequest = new CreateEmployeePayRateRequest(
            employeeId, "Alice Green", 90.00m, "Hourly", "USD",
            DateOnly.FromDateTime(DateTime.UtcNow), null, null
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/pay-rates", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EmployeePayRateDto>();

        var updateRequest = new UpdateEmployeePayRateRequest(
            95.00m, "Hourly", "USD",
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)), "Updated rate"
        );
        var updateResponse = await client.PutAsJsonAsync($"api/timetracking/pay-rates/{created!.Id}", updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<EmployeePayRateDto>();
        updated!.Rate.Should().Be(95.00m);
        updated.Notes.Should().Be("Updated rate");
        updated.EndDate.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Customer Billing Rate Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBillingRate_ReturnsCreated()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var request = new CreateCustomerBillingRateRequest(
            employeeId, "Developer A", companyId, "Acme Corp",
            150.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, "Standard rate"
        );

        var response = await client.PostAsJsonAsync("api/timetracking/billing-rates", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var billingRate = await response.Content.ReadFromJsonAsync<CustomerBillingRateDto>();
        billingRate!.EmployeeId.Should().Be(employeeId);
        billingRate.CompanyId.Should().Be(companyId);
        billingRate.HourlyRate.Should().Be(150.00m);
        billingRate.CompanyName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task GetBillingRatesByEmployee_ReturnsRates()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var company1 = Guid.NewGuid();
        var company2 = Guid.NewGuid();
        var request1 = new CreateCustomerBillingRateRequest(
            employeeId, "Dev B", company1, "Company One", 140.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, null
        );
        var request2 = new CreateCustomerBillingRateRequest(
            employeeId, "Dev B", company2, "Company Two", 160.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, null
        );
        await client.PostAsJsonAsync("api/timetracking/billing-rates", request1);
        await client.PostAsJsonAsync("api/timetracking/billing-rates", request2);

        var response = await client.GetAsync($"api/timetracking/billing-rates/employee/{employeeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rates = await response.Content.ReadFromJsonAsync<List<CustomerBillingRateDto>>();
        rates.Should().HaveCount(2);
        rates.Should().Contain(r => r.CompanyName == "Company One");
        rates.Should().Contain(r => r.CompanyName == "Company Two");
    }

    [Fact]
    public async Task GetBillingRatesByCompany_ReturnsRates()
    {
        var (client, _) = await AuthenticateAsync();
        var companyId = Guid.NewGuid();
        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var request1 = new CreateCustomerBillingRateRequest(
            employee1, "Developer 1", companyId, "Big Client", 120.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, null
        );
        var request2 = new CreateCustomerBillingRateRequest(
            employee2, "Developer 2", companyId, "Big Client", 130.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, null
        );
        await client.PostAsJsonAsync("api/timetracking/billing-rates", request1);
        await client.PostAsJsonAsync("api/timetracking/billing-rates", request2);

        var response = await client.GetAsync($"api/timetracking/billing-rates/company/{companyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rates = await response.Content.ReadFromJsonAsync<List<CustomerBillingRateDto>>();
        rates.Should().HaveCount(2);
        rates.Should().Contain(r => r.EmployeeName == "Developer 1");
        rates.Should().Contain(r => r.EmployeeName == "Developer 2");
    }

    [Fact]
    public async Task GetCurrentBillingRateForEmployeeAndCompany_ReturnsActiveRate()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var oldRequest = new CreateCustomerBillingRateRequest(
            employeeId, "Consultant", companyId, "Tech Inc",
            100.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "Old rate"
        );
        var currentRequest = new CreateCustomerBillingRateRequest(
            employeeId, "Consultant", companyId, "Tech Inc",
            175.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, "New rate"
        );
        await client.PostAsJsonAsync("api/timetracking/billing-rates", oldRequest);
        await client.PostAsJsonAsync("api/timetracking/billing-rates", currentRequest);

        var response = await client.GetAsync($"api/timetracking/billing-rates/employee/{employeeId}/company/{companyId}/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentRate = await response.Content.ReadFromJsonAsync<CustomerBillingRateDto>();
        currentRate!.HourlyRate.Should().Be(175.00m);
        currentRate.Notes.Should().Be("New rate");
    }

    [Fact]
    public async Task UpdateBillingRate_ReturnsUpdatedData()
    {
        var (client, _) = await AuthenticateAsync();
        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var createRequest = new CreateCustomerBillingRateRequest(
            employeeId, "Senior Dev", companyId, "StartupCo",
            200.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), null, null
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/billing-rates", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerBillingRateDto>();

        var updateRequest = new UpdateCustomerBillingRateRequest(
            225.00m, "USD", DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(180)), "Adjusted rate"
        );
        var updateResponse = await client.PutAsJsonAsync($"api/timetracking/billing-rates/{created!.Id}", updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CustomerBillingRateDto>();
        updated!.HourlyRate.Should().Be(225.00m);
        updated.Notes.Should().Be("Adjusted rate");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Time Entry Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateInternalTimeEntry_ReturnsCreatedWithDraftStatus()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Development", "Dev work", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var timeEntryRequest = new CreateTimeEntryRequest(
            employeeId, "Engineer", DateOnly.FromDateTime(DateTime.UtcNow),
            new TimeOnly(9, 0), new TimeOnly(17, 0), 8.0m,
            entryType!.Id, false, null, null, null, "Internal project work"
        );

        var response = await client.PostAsJsonAsync("api/timetracking/entries", timeEntryRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var timeEntry = await response.Content.ReadFromJsonAsync<TimeEntryDto>();
        timeEntry!.EmployeeId.Should().Be(employeeId);
        timeEntry.Hours.Should().Be(8.0m);
        timeEntry.Status.Should().Be("Draft");
        timeEntry.IsBillable.Should().BeFalse();
        timeEntry.CompanyId.Should().BeNull();
    }

    [Fact]
    public async Task CreateCustomerTimeEntry_ReturnsCreatedWithBillingInfo()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Consulting", "Client consulting", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var timeEntryRequest = new CreateTimeEntryRequest(
            employeeId, "Consultant", DateOnly.FromDateTime(DateTime.UtcNow),
            new TimeOnly(10, 0), new TimeOnly(14, 0), 4.0m,
            entryType!.Id, true, companyId, "Client ABC", 150.00m, "Client meeting"
        );

        var response = await client.PostAsJsonAsync("api/timetracking/entries", timeEntryRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var timeEntry = await response.Content.ReadFromJsonAsync<TimeEntryDto>();
        timeEntry!.IsBillable.Should().BeTrue();
        timeEntry.CompanyId.Should().Be(companyId);
        timeEntry.CompanyName.Should().Be("Client ABC");
        timeEntry.BillingRate.Should().Be(150.00m);
        timeEntry.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task GetEntriesWithFilters_ReturnsFilteredResults()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Work", "Work time", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        
        var entry1 = new CreateTimeEntryRequest(
            employee1, "Worker 1", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 5.0m, entryType!.Id, true, companyId, "Company X", 100.00m, null
        );
        var entry2 = new CreateTimeEntryRequest(
            employee2, "Worker 2", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 3.0m, entryType.Id, false, null, null, null, null
        );
        await client.PostAsJsonAsync("api/timetracking/entries", entry1);
        await client.PostAsJsonAsync("api/timetracking/entries", entry2);

        var response = await client.GetAsync($"api/timetracking/entries?employeeId={employee1}&companyId={companyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TimeEntryDto>>();
        result!.Items.Should().HaveCount(1);
        result.Items[0].EmployeeId.Should().Be(employee1);
        result.Items[0].CompanyId.Should().Be(companyId);
    }

    [Fact]
    public async Task GetEntryDetail_ReturnsFullDetail()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Meeting", "Team meetings", 1, true, false);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var timeEntryRequest = new CreateTimeEntryRequest(
            employeeId, "Team Lead", DateOnly.FromDateTime(DateTime.UtcNow),
            new TimeOnly(14, 0), new TimeOnly(15, 30), 1.5m,
            entryType!.Id, false, null, null, null, "Weekly standup"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", timeEntryRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();

        var response = await client.GetAsync($"api/timetracking/entries/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<TimeEntryDetailDto>();
        detail!.Id.Should().Be(created.Id);
        detail.Description.Should().Be("Weekly standup");
        detail.Hours.Should().Be(1.5m);
        detail.Status.Should().Be("Draft");
        detail.CreatedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateTimeEntry_ReturnsUpdatedData()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Coding", "Programming", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var createRequest = new CreateTimeEntryRequest(
            employeeId, "Developer", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 6.0m, entryType!.Id, false, null, null, null, "Initial work"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();

        var updateRequest = new UpdateTimeEntryRequest(
            DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(8, 0), new TimeOnly(16, 0),
            8.0m, entryType.Id, false, null, null, null, "Updated work description"
        );
        var updateResponse = await client.PutAsJsonAsync($"api/timetracking/entries/{created!.Id}", updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<TimeEntryDto>();
        updated!.Hours.Should().Be(8.0m);
        updated.Description.Should().Be("Updated work description");
    }

    [Fact]
    public async Task CreateEntryAndList_AppearsInPaginatedList()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Research", "Research work", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var timeEntryRequest = new CreateTimeEntryRequest(
            employeeId, "Researcher", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 4.0m, entryType!.Id, false, null, null, null, "Research task"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", timeEntryRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();

        var listResponse = await client.GetAsync("api/timetracking/entries?page=1&pageSize=20");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<TimeEntryDto>>();
        result!.Items.Should().Contain(e => e.Id == created!.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Workflow Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubmitTimeEntry_ChangesStatusToSubmitted()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Submit Test", "Test type", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var createRequest = new CreateTimeEntryRequest(
            employeeId, "Worker", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 8.0m, entryType!.Id, false, null, null, null, "Work to submit"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();

        var submitResponse = await client.PostAsync($"api/timetracking/entries/{created!.Id}/submit", null);

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<TimeEntryDto>();
        submitted!.Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task ApproveTimeEntry_ChangesStatusToApprovedWithApprover()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Approve Test", "Test type", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var createRequest = new CreateTimeEntryRequest(
            employeeId, "Worker", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 7.0m, entryType!.Id, false, null, null, null, "Work to approve"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();
        
        await client.PostAsync($"api/timetracking/entries/{created!.Id}/submit", null);

        var approveResponse = await client.PostAsync($"api/timetracking/entries/{created.Id}/approve", null);

        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approveResponse.Content.ReadFromJsonAsync<TimeEntryDto>();
        approved!.Status.Should().Be("Approved");
        
        var detailResponse = await client.GetAsync($"api/timetracking/entries/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<TimeEntryDetailDto>();
        detail!.ApprovedById.Should().NotBeNullOrEmpty();
        detail.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectTimeEntry_ChangesStatusToRejectedWithReason()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Reject Test", "Test type", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var createRequest = new CreateTimeEntryRequest(
            employeeId, "Worker", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 10.0m, entryType!.Id, false, null, null, null, "Work to reject"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();
        
        await client.PostAsync($"api/timetracking/entries/{created!.Id}/submit", null);

        var rejectRequest = new { Reason = "Hours are incorrect" };
        var rejectResponse = await client.PostAsJsonAsync($"api/timetracking/entries/{created.Id}/reject", rejectRequest);

        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<TimeEntryDto>();
        rejected!.Status.Should().Be("Rejected");
        
        var detailResponse = await client.GetAsync($"api/timetracking/entries/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<TimeEntryDetailDto>();
        detail!.RejectionReason.Should().Be("Hours are incorrect");
    }

    [Fact]
    public async Task ApproveDraftEntry_ReturnsBadRequest()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Draft Test", "Test type", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var createRequest = new CreateTimeEntryRequest(
            employeeId, "Worker", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 5.0m, entryType!.Id, false, null, null, null, "Draft work"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();

        var approveResponse = await client.PostAsync($"api/timetracking/entries/{created!.Id}/approve", null);

        approveResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitAlreadySubmittedEntry_ReturnsBadRequest()
    {
        var (client, _) = await AuthenticateAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Double Submit", "Test type", 1, true, true);
        var typeResponse = await client.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var createRequest = new CreateTimeEntryRequest(
            employeeId, "Worker", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 6.0m, entryType!.Id, false, null, null, null, "Double submit test"
        );
        var createResponse = await client.PostAsJsonAsync("api/timetracking/entries", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();
        
        await client.PostAsync($"api/timetracking/entries/{created!.Id}/submit", null);

        var secondSubmitResponse = await client.PostAsync($"api/timetracking/entries/{created.Id}/submit", null);

        secondSubmitResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.NotFound);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Admin Delete Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminCanDeleteEntryType_ReturnsNoContent()
    {
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var createRequest = new CreateTimeEntryTypeRequest("To Delete", "Will be deleted", 99, true, true);
        var createResponse = await adminClient.PostAsJsonAsync("api/timetracking/entry-types", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var deleteResponse = await adminClient.DeleteAsync($"api/timetracking/entry-types/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await adminClient.GetAsync($"api/timetracking/entry-types/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminCanDeleteTimeEntry_ReturnsNoContent()
    {
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var typeRequest = new CreateTimeEntryTypeRequest("Admin Delete", "Test type", 1, true, true);
        var typeResponse = await adminClient.PostAsJsonAsync("api/timetracking/entry-types", typeRequest);
        var entryType = await typeResponse.Content.ReadFromJsonAsync<TimeEntryTypeDto>();

        var employeeId = Guid.NewGuid();
        var createRequest = new CreateTimeEntryRequest(
            employeeId, "Worker", DateOnly.FromDateTime(DateTime.UtcNow),
            null, null, 4.0m, entryType!.Id, false, null, null, null, "To be deleted"
        );
        var createResponse = await adminClient.PostAsJsonAsync("api/timetracking/entries", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>();

        var deleteResponse = await adminClient.DeleteAsync($"api/timetracking/entries/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        var getResponse = await adminClient.GetAsync($"api/timetracking/entries/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminCanDeletePayRate_ReturnsNoContent()
    {
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var employeeId = Guid.NewGuid();
        var createRequest = new CreateEmployeePayRateRequest(
            employeeId, "Test Employee", 50.00m, "Hourly", "USD",
            DateOnly.FromDateTime(DateTime.UtcNow), null, "To be deleted"
        );
        var createResponse = await adminClient.PostAsJsonAsync("api/timetracking/pay-rates", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EmployeePayRateDto>();

        var deleteResponse = await adminClient.DeleteAsync($"api/timetracking/pay-rates/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
