using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Catalog;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Invoicing;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Invoicing;

[Collection("Sequential")]
public class InvoicingControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public InvoicingControllerTests(WarpTestFactory factory) => _factory = factory;

    // ── Auth helpers ──────────────────────────────────────────────────────

    private async Task<(HttpClient client, string token)> AuthenticateAsync(string? email = null)
    {
        var client = _factory.CreateClient();
        email ??= $"inv-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        return (client, token);
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = $"inv-admin-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return (client, auth.Token);
    }

    // ── Invoice helpers ───────────────────────────────────────────────────

    private static async Task<InvoiceDto> CreateDraftInvoiceAsync(HttpClient client, string? companyName = null)
    {
        var request = new CreateInvoiceRequest(
            CompanyName: companyName ?? "Test Corp",
            Currency: "USD",
            Notes: "Integration test invoice");

        var response = await client.PostAsJsonAsync("api/invoicing/invoices", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    private static async Task<InvoiceDto> CreateInvoiceWithLineItemAsync(HttpClient client, string? companyName = null)
    {
        var request = new CreateInvoiceRequest(
            CompanyName: companyName ?? "Test Corp",
            Currency: "USD",
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new(Description: "Widget", Quantity: 2, UnitPrice: 50.00m)
            });

        var response = await client.PostAsJsonAsync("api/invoicing/invoices", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    private static async Task<InvoiceDto> CreateSentInvoiceAsync(HttpClient client, string? companyName = null)
    {
        var invoice = await CreateInvoiceWithLineItemAsync(client, companyName);
        var sendResponse = await client.PostAsync($"api/invoicing/invoices/{invoice.Id}/send", null);
        sendResponse.EnsureSuccessStatusCode();
        return (await sendResponse.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    private static async Task<InvoiceLineItemDto> AddManualLineItemAsync(
        HttpClient client, Guid invoiceId, string description = "Consulting", decimal qty = 1, decimal price = 100)
    {
        var request = new CreateInvoiceLineItemRequest(
            LineItemType: "Manual",
            Description: description,
            Quantity: qty,
            UnitPrice: price);

        var response = await client.PostAsJsonAsync($"api/invoicing/invoices/{invoiceId}/line-items", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceLineItemDto>())!;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  InvoicesController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateInvoice_Returns201()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var request = new CreateInvoiceRequest(CompanyName: "Acme Inc", Currency: "USD");

        // Act
        var response = await client.PostAsJsonAsync("api/invoicing/invoices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceDto>();
        invoice.Should().NotBeNull();
        invoice!.CompanyName.Should().Be("Acme Inc");
        invoice.Status.Should().Be("Draft");
        invoice.InvoiceNumber.Should().NotBeNullOrWhiteSpace();
        invoice.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInvoices_ReturnsPagedList()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        await CreateDraftInvoiceAsync(client, "Paged Corp A");
        await CreateDraftInvoiceAsync(client, "Paged Corp B");

        // Act
        var response = await client.GetAsync("api/invoicing/invoices?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<InvoiceDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetInvoice_ReturnsDetail()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var created = await CreateInvoiceWithLineItemAsync(client, "Detail Corp");

        // Act
        var response = await client.GetAsync($"api/invoicing/invoices/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<InvoiceDetailDto>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(created.Id);
        detail.CompanyName.Should().Be("Detail Corp");
        detail.LineItems.Should().HaveCount(1);
        detail.LineItems[0].Description.Should().Be("Widget");
    }

    [Fact]
    public async Task UpdateInvoice_DraftOnly()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var created = await CreateDraftInvoiceAsync(client, "Old Name");
        var updateRequest = new UpdateInvoiceRequest(
            CompanyId: null,
            CompanyName: "New Name",
            ContactId: null,
            ContactName: null,
            BillingAddress: "123 Main St",
            ShippingAddress: null,
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            PaymentTerms: "Net 30",
            Currency: "USD",
            DiscountPercent: null,
            DiscountFixed: null,
            TaxRate: null,
            Notes: "Updated",
            CustomerNotes: null,
            FooterText: null);

        // Act
        var response = await client.PutAsJsonAsync($"api/invoicing/invoices/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<InvoiceDto>();
        updated!.CompanyName.Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteInvoice_RequiresAdmin_Returns204ForAdmin()
    {
        // Arrange
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var created = await CreateDraftInvoiceAsync(adminClient, "Delete Me Corp");

        // Act
        var response = await adminClient.DeleteAsync($"api/invoicing/invoices/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteInvoice_RequiresAdmin_Returns403ForNonAdmin()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var created = await CreateDraftInvoiceAsync(client, "Forbidden Delete Corp");

        // Act
        var response = await client.DeleteAsync($"api/invoicing/invoices/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendInvoice_TransitionsDraftToSent()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateInvoiceWithLineItemAsync(client, "Send Corp");

        // Act
        var response = await client.PostAsync($"api/invoicing/invoices/{invoice.Id}/send", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sent = await response.Content.ReadFromJsonAsync<InvoiceDto>();
        sent!.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task CancelInvoice_FromDraft()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateDraftInvoiceAsync(client, "Cancel Corp");

        // Act
        var response = await client.PostAsync($"api/invoicing/invoices/{invoice.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadFromJsonAsync<InvoiceDto>();
        cancelled!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task GetSummary_ReturnsDashboardStats()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        await CreateDraftInvoiceAsync(client, "Summary Corp");

        // Act
        var response = await client.GetAsync("api/invoicing/invoices/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<InvoiceSummaryDto>();
        summary.Should().NotBeNull();
        summary!.TotalInvoices.Should().BeGreaterThanOrEqualTo(1);
        summary.DraftCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/invoicing/invoices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  InvoiceLineItemsController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddLineItem_Manual()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateDraftInvoiceAsync(client);

        // Act
        var request = new CreateInvoiceLineItemRequest(
            LineItemType: "Manual",
            Description: "Consulting hour",
            Quantity: 3,
            UnitPrice: 150.00m,
            UnitOfMeasure: "hour");
        var response = await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/line-items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<InvoiceLineItemDto>();
        item!.LineItemType.Should().Be("Manual");
        item.Description.Should().Be("Consulting hour");
        item.Quantity.Should().Be(3);
        item.UnitPrice.Should().Be(150.00m);
        item.LineTotal.Should().Be(450.00m);
    }

    [Fact]
    public async Task AddLineItem_CatalogProduct()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateDraftInvoiceAsync(client);
        var productId = Guid.NewGuid();

        // Act
        var request = new CreateInvoiceLineItemRequest(
            LineItemType: "CatalogProduct",
            Description: "Premium Widget",
            ProductId: productId,
            ProductName: "Premium Widget",
            ProductSku: "WDG-001",
            Quantity: 5,
            UnitPrice: 29.99m);
        var response = await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/line-items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<InvoiceLineItemDto>();
        item!.LineItemType.Should().Be("CatalogProduct");
        item.ProductId.Should().Be(productId);
        item.ProductName.Should().Be("Premium Widget");
        item.ProductSku.Should().Be("WDG-001");
        item.Quantity.Should().Be(5);
        item.LineTotal.Should().Be(149.95m);
    }

    [Fact]
    public async Task AddLineItem_TimeEntry()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateDraftInvoiceAsync(client);
        var timeEntryId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        // Act
        var request = new CreateInvoiceLineItemRequest(
            LineItemType: "TimeEntry",
            Description: "Development work",
            TimeEntryId: timeEntryId,
            EmployeeId: employeeId,
            EmployeeName: "Jane Dev",
            ServiceDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Hours: 8.0m,
            Quantity: 8,
            UnitPrice: 125.00m,
            UnitOfMeasure: "hour");
        var response = await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/line-items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<InvoiceLineItemDto>();
        item!.LineItemType.Should().Be("TimeEntry");
        item.TimeEntryId.Should().Be(timeEntryId);
        item.EmployeeName.Should().Be("Jane Dev");
        item.Hours.Should().Be(8.0m);
        item.LineTotal.Should().Be(1000.00m);
    }

    [Fact]
    public async Task GetLineItems_ReturnsAll()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateDraftInvoiceAsync(client);
        await AddManualLineItemAsync(client, invoice.Id, "Item A", 1, 100);
        await AddManualLineItemAsync(client, invoice.Id, "Item B", 2, 50);

        // Act
        var response = await client.GetAsync($"api/invoicing/invoices/{invoice.Id}/line-items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<InvoiceLineItemDto>>();
        items.Should().HaveCount(2);
        items!.Should().Contain(i => i.Description == "Item A");
        items.Should().Contain(i => i.Description == "Item B");
    }

    [Fact]
    public async Task UpdateLineItem_ReturnsUpdatedData()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateDraftInvoiceAsync(client);
        var lineItem = await AddManualLineItemAsync(client, invoice.Id, "Original", 1, 100);

        var updateRequest = new UpdateInvoiceLineItemRequest(
            Description: "Updated Item",
            Quantity: 3,
            UnitOfMeasure: "each",
            UnitPrice: 75.00m,
            DiscountPercent: null,
            DiscountAmount: null,
            IsTaxable: true);

        // Act
        var response = await client.PutAsJsonAsync(
            $"api/invoicing/invoices/{invoice.Id}/line-items/{lineItem.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<InvoiceLineItemDto>();
        updated!.Description.Should().Be("Updated Item");
        updated.Quantity.Should().Be(3);
        updated.UnitPrice.Should().Be(75.00m);
        updated.LineTotal.Should().Be(225.00m);
    }

    [Fact]
    public async Task DeleteLineItem_ReturnsNoContent()
    {
        // Arrange — DELETE on line items requires Admin
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var invoice = await CreateDraftInvoiceAsync(adminClient);
        var lineItem = await AddManualLineItemAsync(adminClient, invoice.Id, "Delete Me", 1, 50);

        // Act
        var response = await adminClient.DeleteAsync(
            $"api/invoicing/invoices/{invoice.Id}/line-items/{lineItem.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddLineItem_RecalculatesTotals()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateDraftInvoiceAsync(client);

        // Act — add a line item worth $200
        await AddManualLineItemAsync(client, invoice.Id, "Service A", 2, 100);

        // Assert — fetch full invoice detail and verify totals
        var detailResponse = await client.GetAsync($"api/invoicing/invoices/{invoice.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<InvoiceDetailDto>();
        detail!.Subtotal.Should().Be(200.00m);
        detail.TotalAmount.Should().Be(200.00m);
        detail.LineItems.Should().HaveCount(1);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  InvoicePaymentsController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RecordPayment_Returns201()
    {
        // Arrange — invoice must be Sent to accept payments
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateSentInvoiceAsync(client, "Payment Corp");

        var paymentRequest = new CreateInvoicePaymentRequest(
            Amount: 50.00m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod: "BankTransfer",
            ReferenceNumber: "TXN-001");

        // Act
        var response = await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/payments", paymentRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = await response.Content.ReadFromJsonAsync<InvoicePaymentDto>();
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(50.00m);
        payment.PaymentMethod.Should().Be("BankTransfer");
        payment.ReferenceNumber.Should().Be("TXN-001");
    }

    [Fact]
    public async Task RecordPayment_UpdatesInvoiceStatus()
    {
        // Arrange — create sent invoice (2 x $50 = $100)
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateSentInvoiceAsync(client, "Status Corp");

        // Act — partial payment
        var partialPayment = new CreateInvoicePaymentRequest(
            Amount: 30.00m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow));
        await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/payments", partialPayment);

        // Assert — status should be PartiallyPaid
        var detailResponse = await client.GetAsync($"api/invoicing/invoices/{invoice.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<InvoiceDetailDto>();
        detail!.Status.Should().Be("PartiallyPaid");
        detail.AmountPaid.Should().Be(30.00m);

        // Act — full remaining payment
        var fullPayment = new CreateInvoicePaymentRequest(
            Amount: 70.00m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow));
        await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/payments", fullPayment);

        // Assert — status should be Paid
        var finalResponse = await client.GetAsync($"api/invoicing/invoices/{invoice.Id}");
        var finalDetail = await finalResponse.Content.ReadFromJsonAsync<InvoiceDetailDto>();
        finalDetail!.Status.Should().Be("Paid");
        finalDetail.BalanceDue.Should().Be(0);
    }

    [Fact]
    public async Task GetPayments_ReturnsAll()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateSentInvoiceAsync(client, "Multi Payment Corp");

        var pay1 = new CreateInvoicePaymentRequest(Amount: 20.00m, PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow));
        var pay2 = new CreateInvoicePaymentRequest(Amount: 15.00m, PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow));
        await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/payments", pay1);
        await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/payments", pay2);

        // Act
        var response = await client.GetAsync($"api/invoicing/invoices/{invoice.Id}/payments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payments = await response.Content.ReadFromJsonAsync<List<InvoicePaymentDto>>();
        payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeletePayment_RequiresAdmin_Returns403()
    {
        // Arrange — non-admin user records a payment, then tries to delete
        var (client, _) = await AuthenticateAsync();
        var invoice = await CreateSentInvoiceAsync(client, "Forbidden Pay Corp");

        var paymentRequest = new CreateInvoicePaymentRequest(Amount: 10.00m, PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow));
        var createResponse = await client.PostAsJsonAsync($"api/invoicing/invoices/{invoice.Id}/payments", paymentRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<InvoicePaymentDto>();

        // Act
        var response = await client.DeleteAsync($"api/invoicing/invoices/{invoice.Id}/payments/{payment!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  InvoiceSettingsController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSettings_ReturnsDefaults()
    {
        // Arrange — settings requires Admin
        var (adminClient, _) = await AuthenticateAsAdminAsync();

        // Act
        var response = await adminClient.GetAsync("api/invoicing/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<InvoiceSettingsDto>();
        settings.Should().NotBeNull();
        settings!.Prefix.Should().Be("INV-");
        settings.DefaultPaymentTerms.Should().Be("Net 30");
        settings.DefaultCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task UpdateSettings_ReturnsUpdated()
    {
        // Arrange
        var (adminClient, _) = await AuthenticateAsAdminAsync();
        var updateRequest = new UpdateInvoiceSettingsRequest(
            Prefix: "BILL-",
            NumberPadding: 6,
            DefaultPaymentTerms: "Net 60",
            DefaultDueDays: 60,
            DefaultTaxRate: 8.25m,
            DefaultCurrency: "USD",
            DefaultFooterText: "Thank you!",
            DefaultCustomerNotes: "Please pay promptly.",
            CompanyName: "Warp Corp",
            CompanyAddress: "123 Space Blvd",
            CompanyPhone: "555-0100",
            CompanyEmail: "billing@warp.corp",
            CompanyLogoUrl: null);

        // Act
        var response = await adminClient.PutAsJsonAsync("api/invoicing/settings", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<InvoiceSettingsDto>();
        settings!.Prefix.Should().Be("BILL-");
        settings.DefaultPaymentTerms.Should().Be("Net 60");
        settings.DefaultDueDays.Should().Be(60);
        settings.DefaultTaxRate.Should().Be(8.25m);
        settings.CompanyName.Should().Be("Warp Corp");
    }

    [Fact]
    public async Task Settings_RequiresAdmin_Returns403()
    {
        // Arrange — non-admin user
        var (client, _) = await AuthenticateAsync();

        // Act
        var response = await client.GetAsync("api/invoicing/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Catalog Product Search
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SearchProducts_ReturnsResults()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();

        // Act — search with empty query returns empty array
        var response = await client.GetAsync("api/catalog/products/search?q=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<CatalogItemSearchResult>>();
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchProducts_LimitsResults()
    {
        // Arrange
        var (client, _) = await AuthenticateAsync();

        // Act — specify limit parameter
        var response = await client.GetAsync("api/catalog/products/search?q=x&limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<CatalogItemSearchResult>>();
        results.Should().NotBeNull();
        results!.Count.Should().BeLessThanOrEqualTo(5);
    }
}
