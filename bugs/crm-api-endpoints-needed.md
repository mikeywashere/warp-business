# CRM Backend API Endpoints Needed

**Date:** 2026-04-13  
**For:** Data Team  
**From:** Geordi (Frontend Dev)  
**Commit:** 48b534f

## Summary

Frontend CRM UI layer is complete and ready for integration. The following API endpoints are expected by `CrmApiClient.cs`:

## Customer CRUD Endpoints

### GET /api/crm/customers
- **Purpose:** Get all customers for current tenant
- **Auth:** Bearer token + X-Tenant-Id header
- **Response:** `List<CustomerResponse>`

### GET /api/crm/customers/{id}
- **Purpose:** Get single customer by ID
- **Auth:** Bearer token + X-Tenant-Id header
- **Response:** `CustomerResponse` (200) or 404

### POST /api/crm/customers
- **Purpose:** Create new customer
- **Auth:** Bearer token + X-Tenant-Id header
- **Body:** `CreateCustomerRequest` (Name, Email, Phone, Address, City, State, PostalCode, Country, Website, Industry, CompanySize, Notes)
- **Response:** `CustomerResponse` (201)

### PUT /api/crm/customers/{id}
- **Purpose:** Update existing customer
- **Auth:** Bearer token + X-Tenant-Id header
- **Body:** `UpdateCustomerRequest` (same fields as Create)
- **Response:** 204 or updated `CustomerResponse`

### POST /api/crm/customers/{id}/activate
- **Purpose:** Activate a customer (set IsActive = true)
- **Auth:** Bearer token + X-Tenant-Id header
- **Response:** 204

### POST /api/crm/customers/{id}/deactivate
- **Purpose:** Deactivate a customer (set IsActive = false)
- **Auth:** Bearer token + X-Tenant-Id header
- **Response:** 204

## Customer-Employee Relationship Endpoints

### GET /api/crm/customers/{customerId}/employees
- **Purpose:** Get all employees assigned to a customer
- **Auth:** Bearer token + X-Tenant-Id header
- **Response:** `List<CustomerEmployeeResponse>` (includes EmployeeFirstName, EmployeeLastName, EmployeeEmail, Relationship, CustomerId, EmployeeId, CreatedAt)

### POST /api/crm/customers/{customerId}/employees
- **Purpose:** Assign employee to customer
- **Auth:** Bearer token + X-Tenant-Id header
- **Body:** `AssignEmployeeRequest` (EmployeeId, Relationship)
- **Response:** 201

### PUT /api/crm/customers/{customerId}/employees/{employeeId}
- **Purpose:** Update relationship text for customer-employee pair
- **Auth:** Bearer token + X-Tenant-Id header
- **Body:** `UpdateRelationshipRequest` (Relationship)
- **Response:** 204

### DELETE /api/crm/customers/{customerId}/employees/{employeeId}
- **Purpose:** Unassign employee from customer
- **Auth:** Bearer token + X-Tenant-Id header
- **Response:** 204

## Notes

- All DTOs defined in `WarpBusiness.Web/Services/CrmApiClient.cs`
- All endpoints expect multi-tenant context via `X-Tenant-Id` header (handled by `AuthTokenHandler`)
- CompanySize is a string with values: "1-10", "11-50", "51-200", "201-500", "501+"
- Relationship field max length: 100 characters
- Customer fields (Name, Email required; all others optional except TenantId)
- Frontend uses `ApiException` for error handling (parses JSON error bodies with `message`, `detail`, `title` fields)
