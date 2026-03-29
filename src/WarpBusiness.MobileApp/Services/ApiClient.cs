using System.Net.Http.Json;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Catalog;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Invoicing;
using WarpBusiness.Shared.Plugins;
using WarpBusiness.Shared.TimeTracking;

namespace WarpBusiness.MobileApp.Services;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    // ========== ADMIN ==========
    public async Task<List<UserSummaryDto>?> GetUsersAsync()
        => await _http.GetFromJsonAsync<List<UserSummaryDto>>("api/admin/users");

    public async Task SetUserRoleAsync(string userId, string role, bool add)
        => await _http.PostAsJsonAsync($"api/admin/users/{userId}/roles", new SetRoleRequest(role, add));

    public async Task DeleteUserAsync(string userId)
        => await _http.DeleteAsync($"api/admin/users/{userId}");

    public async Task<CreateUserFromEmployeeResponse?> CreateUserFromEmployeeAsync(Guid employeeId)
    {
        var response = await _http.PostAsync($"api/admin/users/from-employee/{employeeId}", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateUserFromEmployeeResponse>();
    }

    // ========== TENANTS ==========
    public async Task<List<TenantSummaryDto>?> GetMyTenantsAsync()
        => await _http.GetFromJsonAsync<List<TenantSummaryDto>>("api/tenants/mine");

    public async Task<TenantDetailDto?> GetTenantAsync(Guid id)
        => await _http.GetFromJsonAsync<TenantDetailDto>($"api/tenants/{id}");

    public async Task UpdateTenantAsync(Guid id, UpdateTenantRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/tenants/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddTenantMemberAsync(Guid tenantId, object request)
    {
        var response = await _http.PostAsJsonAsync($"api/tenants/{tenantId}/members", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveTenantMemberAsync(Guid tenantId, string userId)
        => await _http.DeleteAsync($"api/tenants/{tenantId}/members/{userId}");

    public async Task ChangeMemberRoleAsync(Guid tenantId, string userId, object request)
    {
        var response = await _http.PutAsJsonAsync($"api/tenants/{tenantId}/members/{userId}/role", request);
        response.EnsureSuccessStatusCode();
    }

    // ========== EMPLOYEES ==========
    public async Task<EmployeePagedResult?> GetEmployeesAsync(int page = 1, int pageSize = 20, bool includeInactive = false, string? department = null, string? search = null)
    {
        var url = $"api/employees?page={page}&pageSize={pageSize}&includeInactive={includeInactive}";
        if (!string.IsNullOrEmpty(department)) url += $"&department={Uri.EscapeDataString(department)}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<EmployeePagedResult>(url);
    }

    public async Task<EmployeeDto?> GetEmployeeAsync(Guid id)
        => await _http.GetFromJsonAsync<EmployeeDto>($"api/employees/{id}");

    public async Task<EmployeeDto?> CreateEmployeeAsync(EmployeeRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/employees", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmployeeDto>();
    }

    public async Task UpdateEmployeeAsync(Guid id, EmployeeRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/employees/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeactivateEmployeeAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/employees/{id}/deactivate", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteEmployeeAsync(Guid id)
        => await _http.DeleteAsync($"api/employees/{id}");

    public async Task<List<string>?> GetDepartmentsAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/employees/departments");

    public async Task<List<EmployeeDto>?> GetManagersAsync()
        => await _http.GetFromJsonAsync<List<EmployeeDto>>("api/employees/managers");

    // ========== CRM: COMPANIES ==========
    public async Task<PagedResult<CompanyDto>?> GetCompaniesAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var url = $"api/companies?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<PagedResult<CompanyDto>>(url);
    }

    public async Task<List<CompanyDto>?> SearchCompaniesAsync(string query)
        => await _http.GetFromJsonAsync<List<CompanyDto>>($"api/companies/search?q={Uri.EscapeDataString(query)}");

    public async Task<CompanyDetailDto?> GetCompanyAsync(Guid id)
        => await _http.GetFromJsonAsync<CompanyDetailDto>($"api/companies/{id}");

    public async Task<CompanyDto?> CreateCompanyAsync(CreateCompanyRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/companies", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompanyDto>();
    }

    public async Task UpdateCompanyAsync(Guid id, UpdateCompanyRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/companies/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCompanyAsync(Guid id)
        => await _http.DeleteAsync($"api/companies/{id}");

    // ========== CRM: CONTACTS ==========
    public async Task<PagedResult<ContactDto>?> GetContactsAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var url = $"api/contacts?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<PagedResult<ContactDto>>(url);
    }

    public async Task<ContactDto?> GetMyContactAsync()
        => await _http.GetFromJsonAsync<ContactDto>("api/contacts/me");

    public async Task<ContactDto?> GetContactAsync(Guid id)
        => await _http.GetFromJsonAsync<ContactDto>($"api/contacts/{id}");

    public async Task<ContactDto?> CreateContactAsync(CreateContactRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/contacts", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContactDto>();
    }

    public async Task UpdateContactAsync(Guid id, UpdateContactRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/contacts/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteContactAsync(Guid id)
        => await _http.DeleteAsync($"api/contacts/{id}");

    // ========== CRM: DEALS ==========
    public async Task<PagedResult<DealDto>?> GetDealsAsync(int page = 1, int pageSize = 20, string? stage = null)
    {
        var url = $"api/deals?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(stage)) url += $"&stage={Uri.EscapeDataString(stage)}";
        return await _http.GetFromJsonAsync<PagedResult<DealDto>>(url);
    }

    public async Task<DealPipelineSummary?> GetDealSummaryAsync()
        => await _http.GetFromJsonAsync<DealPipelineSummary>("api/deals/summary");

    public async Task<DealDto?> GetDealAsync(Guid id)
        => await _http.GetFromJsonAsync<DealDto>($"api/deals/{id}");

    public async Task<DealDto?> CreateDealAsync(CreateDealRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/deals", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DealDto>();
    }

    public async Task UpdateDealAsync(Guid id, UpdateDealRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/deals/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDealAsync(Guid id)
        => await _http.DeleteAsync($"api/deals/{id}");

    // ========== CRM: ACTIVITIES ==========
    public async Task<PagedResult<ActivityDto>?> GetActivitiesAsync(int page = 1, int pageSize = 20, Guid? contactId = null, Guid? companyId = null, Guid? dealId = null, bool? isCompleted = null)
    {
        var url = $"api/activities?page={page}&pageSize={pageSize}";
        if (contactId.HasValue) url += $"&contactId={contactId}";
        if (companyId.HasValue) url += $"&companyId={companyId}";
        if (dealId.HasValue) url += $"&dealId={dealId}";
        if (isCompleted.HasValue) url += $"&isCompleted={isCompleted}";
        return await _http.GetFromJsonAsync<PagedResult<ActivityDto>>(url);
    }

    public async Task<ActivityDto?> GetActivityAsync(Guid id)
        => await _http.GetFromJsonAsync<ActivityDto>($"api/activities/{id}");

    public async Task<ActivityDto?> CreateActivityAsync(CreateActivityRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/activities", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityDto>();
    }

    public async Task UpdateActivityAsync(Guid id, UpdateActivityRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/activities/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteActivityAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/activities/{id}/complete", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteActivityAsync(Guid id)
        => await _http.DeleteAsync($"api/activities/{id}");

    // ========== CRM: CUSTOM FIELDS ==========
    public async Task<PagedResult<CustomFieldDefinitionDto>?> GetCustomFieldsAsync(string entityType, int page = 1, int pageSize = 50)
        => await _http.GetFromJsonAsync<PagedResult<CustomFieldDefinitionDto>>($"api/contacts/custom-fields?entityType={entityType}&page={page}&pageSize={pageSize}");

    public async Task<CustomFieldDefinitionDto?> CreateCustomFieldAsync(CreateCustomFieldDefinitionRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/contacts/custom-fields", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>();
    }

    public async Task UpdateCustomFieldAsync(Guid id, UpdateCustomFieldDefinitionRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/contacts/custom-fields/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCustomFieldAsync(Guid id)
        => await _http.DeleteAsync($"api/contacts/custom-fields/{id}");

    // ========== CATALOG: PRODUCTS ==========
    public async Task<PagedResult<ProductDto>?> GetProductsAsync(int page = 1, int pageSize = 20, string? search = null, Guid? categoryId = null, string? status = null)
    {
        var url = $"api/catalog/products?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
        return await _http.GetFromJsonAsync<PagedResult<ProductDto>>(url);
    }

    public async Task<List<CatalogItemSearchResult>?> SearchProductsAsync(string query)
        => await _http.GetFromJsonAsync<List<CatalogItemSearchResult>>($"api/catalog/products/search?q={Uri.EscapeDataString(query)}");

    public async Task<ProductDetailDto?> GetProductAsync(Guid id)
        => await _http.GetFromJsonAsync<ProductDetailDto>($"api/catalog/products/{id}");

    public async Task<ProductDto?> CreateProductAsync(CreateProductRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/catalog/products", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }

    public async Task UpdateProductAsync(Guid id, UpdateProductRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/catalog/products/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProductAsync(Guid id)
        => await _http.DeleteAsync($"api/catalog/products/{id}");

    // Product Images
    public async Task<ProductImageDto?> CreateProductImageAsync(Guid productId, CreateProductImageRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/catalog/products/{productId}/images", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductImageDto>();
    }

    public async Task DeleteProductImageAsync(Guid productId, Guid imageId)
        => await _http.DeleteAsync($"api/catalog/products/{productId}/images/{imageId}");

    // Product Ingredients
    public async Task<ProductIngredientDto?> CreateProductIngredientAsync(Guid productId, CreateProductIngredientRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/catalog/products/{productId}/ingredients", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductIngredientDto>();
    }

    public async Task UpdateProductIngredientAsync(Guid productId, Guid ingredientId, UpdateProductIngredientRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/catalog/products/{productId}/ingredients/{ingredientId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProductIngredientAsync(Guid productId, Guid ingredientId)
        => await _http.DeleteAsync($"api/catalog/products/{productId}/ingredients/{ingredientId}");

    // Product Options
    public async Task<ProductOptionDto?> CreateProductOptionAsync(Guid productId, CreateProductOptionRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/catalog/products/{productId}/options", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductOptionDto>();
    }

    public async Task DeleteProductOptionAsync(Guid productId, Guid optionId)
        => await _http.DeleteAsync($"api/catalog/products/{productId}/options/{optionId}");

    // Product Variants
    public async Task<ProductVariantDto?> CreateProductVariantAsync(Guid productId, CreateProductVariantRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/catalog/products/{productId}/variants", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductVariantDto>();
    }

    public async Task UpdateProductVariantAsync(Guid productId, Guid variantId, UpdateProductVariantRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/catalog/products/{productId}/variants/{variantId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProductVariantAsync(Guid productId, Guid variantId)
        => await _http.DeleteAsync($"api/catalog/products/{productId}/variants/{variantId}");

    // ========== CATALOG: CATEGORIES ==========
    public async Task<PagedResult<CategoryDto>?> GetCategoriesAsync(int page = 1, int pageSize = 20)
        => await _http.GetFromJsonAsync<PagedResult<CategoryDto>>($"api/catalog/categories?page={page}&pageSize={pageSize}");

    public async Task<List<CategoryDto>?> GetAllCategoriesAsync()
        => await _http.GetFromJsonAsync<List<CategoryDto>>("api/catalog/categories/all");

    public async Task<CategoryDetailDto?> GetCategoryAsync(Guid id)
        => await _http.GetFromJsonAsync<CategoryDetailDto>($"api/catalog/categories/{id}");

    public async Task<CategoryDto?> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/catalog/categories", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CategoryDto>();
    }

    public async Task UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/catalog/categories/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCategoryAsync(Guid id)
        => await _http.DeleteAsync($"api/catalog/categories/{id}");

    // ========== INVOICING ==========
    public async Task<PagedResult<InvoiceDto>?> GetInvoicesAsync(int page = 1, int pageSize = 20, Guid? companyId = null, string? status = null, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var url = $"api/invoicing/invoices?page={page}&pageSize={pageSize}";
        if (companyId.HasValue) url += $"&companyId={companyId}";
        if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
        if (fromDate.HasValue) url += $"&fromDate={fromDate:yyyy-MM-dd}";
        if (toDate.HasValue) url += $"&toDate={toDate:yyyy-MM-dd}";
        return await _http.GetFromJsonAsync<PagedResult<InvoiceDto>>(url);
    }

    public async Task<InvoiceSummaryDto?> GetInvoiceSummaryAsync()
        => await _http.GetFromJsonAsync<InvoiceSummaryDto>("api/invoicing/invoices/summary");

    public async Task<InvoiceDetailDto?> GetInvoiceAsync(Guid id)
        => await _http.GetFromJsonAsync<InvoiceDetailDto>($"api/invoicing/invoices/{id}");

    public async Task<InvoiceDto?> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/invoicing/invoices", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvoiceDto>();
    }

    public async Task UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/invoicing/invoices/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteInvoiceAsync(Guid id)
        => await _http.DeleteAsync($"api/invoicing/invoices/{id}");

    public async Task SendInvoiceAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/invoicing/invoices/{id}/send", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelInvoiceAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/invoicing/invoices/{id}/cancel", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task VoidInvoiceAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/invoicing/invoices/{id}/void", null);
        response.EnsureSuccessStatusCode();
    }

    // Invoice Line Items
    public async Task<InvoiceLineItemDto?> AddLineItemAsync(Guid invoiceId, CreateInvoiceLineItemRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/invoicing/invoices/{invoiceId}/line-items", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvoiceLineItemDto>();
    }

    public async Task UpdateLineItemAsync(Guid invoiceId, Guid lineItemId, UpdateInvoiceLineItemRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/invoicing/invoices/{invoiceId}/line-items/{lineItemId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteLineItemAsync(Guid invoiceId, Guid lineItemId)
        => await _http.DeleteAsync($"api/invoicing/invoices/{invoiceId}/line-items/{lineItemId}");

    // Invoice Payments
    public async Task<InvoicePaymentDto?> AddPaymentAsync(Guid invoiceId, CreateInvoicePaymentRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/invoicing/invoices/{invoiceId}/payments", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvoicePaymentDto>();
    }

    public async Task DeletePaymentAsync(Guid invoiceId, Guid paymentId)
        => await _http.DeleteAsync($"api/invoicing/invoices/{invoiceId}/payments/{paymentId}");

    // Invoice Settings
    public async Task<InvoiceSettingsDto?> GetInvoiceSettingsAsync()
        => await _http.GetFromJsonAsync<InvoiceSettingsDto>("api/invoicing/settings");

    public async Task UpdateInvoiceSettingsAsync(UpdateInvoiceSettingsRequest request)
    {
        var response = await _http.PutAsJsonAsync("api/invoicing/settings", request);
        response.EnsureSuccessStatusCode();
    }

    // ========== TIME TRACKING: ENTRIES ==========
    public async Task<PagedResult<TimeEntryDto>?> GetTimeEntriesAsync(int page = 1, int pageSize = 20, Guid? employeeId = null, Guid? companyId = null, string? status = null, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var url = $"api/timetracking/entries?page={page}&pageSize={pageSize}";
        if (employeeId.HasValue) url += $"&employeeId={employeeId}";
        if (companyId.HasValue) url += $"&companyId={companyId}";
        if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
        if (fromDate.HasValue) url += $"&fromDate={fromDate:yyyy-MM-dd}";
        if (toDate.HasValue) url += $"&toDate={toDate:yyyy-MM-dd}";
        return await _http.GetFromJsonAsync<PagedResult<TimeEntryDto>>(url);
    }

    public async Task<TimeEntryDetailDto?> GetTimeEntryAsync(Guid id)
        => await _http.GetFromJsonAsync<TimeEntryDetailDto>($"api/timetracking/entries/{id}");

    public async Task<TimeEntryDto?> CreateTimeEntryAsync(CreateTimeEntryRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/timetracking/entries", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TimeEntryDto>();
    }

    public async Task UpdateTimeEntryAsync(Guid id, UpdateTimeEntryRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/timetracking/entries/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTimeEntryAsync(Guid id)
        => await _http.DeleteAsync($"api/timetracking/entries/{id}");

    public async Task SubmitTimeEntryAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/timetracking/entries/{id}/submit", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApproveTimeEntryAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/timetracking/entries/{id}/approve", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task RejectTimeEntryAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/timetracking/entries/{id}/reject", null);
        response.EnsureSuccessStatusCode();
    }

    // ========== TIME TRACKING: ENTRY TYPES ==========
    public async Task<List<TimeEntryTypeDto>?> GetTimeEntryTypesAsync(bool includeInactive = false)
        => await _http.GetFromJsonAsync<List<TimeEntryTypeDto>>($"api/timetracking/entry-types?includeInactive={includeInactive}");

    public async Task<TimeEntryTypeDto?> GetTimeEntryTypeAsync(Guid id)
        => await _http.GetFromJsonAsync<TimeEntryTypeDto>($"api/timetracking/entry-types/{id}");

    public async Task<TimeEntryTypeDto?> CreateTimeEntryTypeAsync(CreateTimeEntryTypeRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/timetracking/entry-types", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TimeEntryTypeDto>();
    }

    public async Task UpdateTimeEntryTypeAsync(Guid id, UpdateTimeEntryTypeRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/timetracking/entry-types/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTimeEntryTypeAsync(Guid id)
        => await _http.DeleteAsync($"api/timetracking/entry-types/{id}");

    // ========== TIME TRACKING: PAY RATES ==========
    public async Task<List<EmployeePayRateDto>?> GetPayRatesAsync(Guid employeeId)
        => await _http.GetFromJsonAsync<List<EmployeePayRateDto>>($"api/timetracking/pay-rates/employee/{employeeId}");

    public async Task<EmployeePayRateDto?> GetCurrentPayRateAsync(Guid employeeId)
        => await _http.GetFromJsonAsync<EmployeePayRateDto>($"api/timetracking/pay-rates/employee/{employeeId}/current");

    public async Task<EmployeePayRateDto?> CreatePayRateAsync(CreateEmployeePayRateRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/timetracking/pay-rates", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmployeePayRateDto>();
    }

    public async Task UpdatePayRateAsync(Guid id, UpdateEmployeePayRateRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/timetracking/pay-rates/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePayRateAsync(Guid id)
        => await _http.DeleteAsync($"api/timetracking/pay-rates/{id}");

    // ========== TIME TRACKING: BILLING RATES ==========
    public async Task<List<CustomerBillingRateDto>?> GetBillingRatesByEmployeeAsync(Guid employeeId)
        => await _http.GetFromJsonAsync<List<CustomerBillingRateDto>>($"api/timetracking/billing-rates/employee/{employeeId}");

    public async Task<List<CustomerBillingRateDto>?> GetBillingRatesByCompanyAsync(Guid companyId)
        => await _http.GetFromJsonAsync<List<CustomerBillingRateDto>>($"api/timetracking/billing-rates/company/{companyId}");

    public async Task<CustomerBillingRateDto?> GetCurrentBillingRateAsync(Guid employeeId, Guid companyId)
        => await _http.GetFromJsonAsync<CustomerBillingRateDto>($"api/timetracking/billing-rates/employee/{employeeId}/company/{companyId}/current");

    public async Task<CustomerBillingRateDto?> CreateBillingRateAsync(CreateCustomerBillingRateRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/timetracking/billing-rates", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerBillingRateDto>();
    }

    public async Task UpdateBillingRateAsync(Guid id, UpdateCustomerBillingRateRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/timetracking/billing-rates/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteBillingRateAsync(Guid id)
        => await _http.DeleteAsync($"api/timetracking/billing-rates/{id}");

    // ========== CONTACT RELATIONSHIPS ==========
    public async Task<List<ContactEmployeeRelationshipDto>?> GetContactRelationshipsAsync(Guid contactId)
        => await _http.GetFromJsonAsync<List<ContactEmployeeRelationshipDto>>($"api/contacts/{contactId}/relationships");

    public async Task<ContactEmployeeRelationshipDto?> CreateContactRelationshipAsync(Guid contactId, CreateContactEmployeeRelationshipRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/contacts/{contactId}/relationships", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipDto>();
    }

    public async Task DeleteContactRelationshipAsync(Guid contactId, Guid relationshipId)
        => await _http.DeleteAsync($"api/contacts/{contactId}/relationships/{relationshipId}");

    // Relationship Types
    public async Task<List<ContactEmployeeRelationshipTypeDto>?> GetRelationshipTypesAsync()
        => await _http.GetFromJsonAsync<List<ContactEmployeeRelationshipTypeDto>>("api/contacts/relationship-types");

    public async Task<ContactEmployeeRelationshipTypeDto?> CreateRelationshipTypeAsync(CreateContactEmployeeRelationshipTypeRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/contacts/relationship-types", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContactEmployeeRelationshipTypeDto>();
    }

    public async Task UpdateRelationshipTypeAsync(Guid id, UpdateContactEmployeeRelationshipTypeRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/contacts/relationship-types/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteRelationshipTypeAsync(Guid id)
        => await _http.DeleteAsync($"api/contacts/relationship-types/{id}");
}
