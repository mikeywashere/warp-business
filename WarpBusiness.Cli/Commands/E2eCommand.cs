using System.CommandLine;
using WarpBusiness.Cli.Data;
using WarpBusiness.Cli.Services;

namespace WarpBusiness.Cli.Commands;

public static class E2eCommand
{
    private static readonly Option<int> TenantCountOption = new(
        "--tenantCount",
        getDefaultValue: () => 1,
        description: "Number of tenants to create");

    private static readonly Option<int> EmployeeCountOption = new(
        "--employeeCount",
        getDefaultValue: () => 100,
        description: "Number of employees to create per tenant");

    private static readonly Option<int> CustomerCountOption = new(
        "--customerCount",
        getDefaultValue: () => 50,
        description: "Number of customers (and businesses) to create per tenant");

    public static Command Create()
    {
        var cmd = new Command("e2e", "Seed the database with realistic test data via the API");
        cmd.AddOption(TenantCountOption);
        cmd.AddOption(EmployeeCountOption);
        cmd.AddOption(CustomerCountOption);
        cmd.SetHandler(HandleAsync, TenantCountOption, EmployeeCountOption, CustomerCountOption);
        return cmd;
    }

    private static async Task HandleAsync(int tenantCount, int employeeCount, int customerCount)
    {
        var token = TokenStore.Load();
        if (token is null)
        {
            Console.Error.WriteLine("❌ Not logged in. Run 'warp login' first.");
            return;
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Console.Error.WriteLine("❌ Session expired. Run 'warp login' to re-authenticate.");
            return;
        }

        var apiClient = new WarpApiClient(token.ApiUrl, token.AccessToken);
        var rng = new Random();

        Console.WriteLine("🚀 WarpBusiness E2E Data Seeder");
        Console.WriteLine($"   Tenants: {tenantCount} | Employees/tenant: {employeeCount} | Customers/tenant: {customerCount}");

        int totalEmployees = 0;
        int totalCustomers = 0;
        int totalBusinesses = 0;

        for (int t = 1; t <= tenantCount; t++)
        {
            // ── Generate unique tenant name ──────────────────────────────────
            string tenantName;
            string tenantSlug;
            TenantResult? tenant = null;

            do
            {
                var adj = WordData.Adjectives[rng.Next(WordData.Adjectives.Length)];
                var noun = WordData.Nouns[rng.Next(WordData.Nouns.Length)];
                var suffix = rng.Next(1000, 9999);
                tenantName = $"{adj}-{noun}-{suffix}";
                tenantSlug = tenantName;

                Console.WriteLine($"\n[{t}/{tenantCount}] Creating tenant '{tenantName}'...");

                try
                {
                    tenant = await apiClient.CreateTenantAsync(tenantName, tenantSlug);
                    if (tenant is null)
                        Console.WriteLine("  ⚠️  Name conflict, retrying...");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  ⚠️  Warning: Tenant creation failed: {ex.Message}");
                    break;
                }
            } while (tenant is null);

            if (tenant is null)
                continue;

            Console.WriteLine($"  ✅ Tenant created: {tenant.Name} ({tenant.Id})");

            // ── Employees ───────────────────────────────────────────────────
            Console.WriteLine($"  👥 Creating {employeeCount} employees...");

            for (int e = 1; e <= employeeCount; e++)
            {
                var firstName = NameData.FirstNames[rng.Next(NameData.FirstNames.Length)];
                var lastName = NameData.LastNames[rng.Next(NameData.LastNames.Length)];
                var emailSuffix = rng.Next(1000, 9999);
                var email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}.{emailSuffix}@{tenant.Slug}.e2e";

                var city = LocationData.Cities[rng.Next(LocationData.Cities.Length)];
                var streetName = WordData.StreetNames[rng.Next(WordData.StreetNames.Length)];
                var streetType = WordData.StreetTypes[rng.Next(WordData.StreetTypes.Length)];
                var streetNumber = rng.Next(1, 999);
                var address = $"{streetNumber} {streetName} {streetType}";
                var zip = $"{city.ZipPrefix}{rng.Next(10, 99):D2}";

                var department = WordData.Departments[rng.Next(WordData.Departments.Length)];
                var titles = WordData.JobTitlesByDepartment[department];
                var jobTitle = titles[rng.Next(titles.Length)];

                var phone = $"(555) {rng.Next(100, 999)}-{rng.Next(1000, 9999)}";
                var pay = Math.Round((decimal)(rng.Next(45, 181) * 1000), 0);

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var hireDate = today.AddDays(-rng.Next(0, 365 * 5));
                var dob = today.AddYears(-rng.Next(25, 61)).AddDays(-rng.Next(0, 365));

                EmployeeResult? emp = null;
                int retries = 0;
                while (emp is null && retries < 5)
                {
                    if (retries > 0)
                    {
                        emailSuffix = rng.Next(1000, 9999);
                        email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}.{emailSuffix}@{tenant.Slug}.e2e";
                    }

                    try
                    {
                        emp = await apiClient.CreateEmployeeAsync(tenant.Id, new CreateEmployeeRequest(
                            firstName, lastName, email, phone,
                            dob, hireDate, department, jobTitle,
                            "Active", "FullTime", pay, "Salary", "USD"));

                        if (emp is null) retries++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  ⚠️  Warning: Employee {firstName} {lastName} failed: {ex.Message}");
                        break;
                    }
                }

                if (e % 10 == 0)
                    Console.WriteLine($"    {e}/{employeeCount} employees created...");
            }

            totalEmployees += employeeCount;
            Console.WriteLine($"  ✅ {employeeCount} employees created");

            // ── Customers + Businesses ──────────────────────────────────────
            Console.WriteLine($"  🏢 Creating {customerCount} customers (+ {customerCount} businesses)...");

            var companySizes = new[] { "1-10", "11-50", "51-200", "201-500", "501+" };

            for (int c = 1; c <= customerCount; c++)
            {
                // Business
                var adj = WordData.Adjectives[rng.Next(WordData.Adjectives.Length)];
                var noun = WordData.Nouns[rng.Next(WordData.Nouns.Length)];
                var suffix = WordData.CompanySuffixes[rng.Next(WordData.CompanySuffixes.Length)];
                var bizName = $"{char.ToUpper(adj[0])}{adj[1..]} {char.ToUpper(noun[0])}{noun[1..]} {suffix}";
                var industry = WordData.Industries[rng.Next(WordData.Industries.Length)];

                var bizCity = LocationData.Cities[rng.Next(LocationData.Cities.Length)];
                var bizStreetName = WordData.StreetNames[rng.Next(WordData.StreetNames.Length)];
                var bizStreetType = WordData.StreetTypes[rng.Next(WordData.StreetTypes.Length)];
                var bizAddress = $"{rng.Next(1, 999)} {bizStreetName} {bizStreetType}";
                var bizZip = $"{bizCity.ZipPrefix}{rng.Next(10, 99):D2}";
                var bizPhone = $"(555) {rng.Next(100, 999)}-{rng.Next(1000, 9999)}";

                BusinessResult? biz = null;
                int bizRetries = 0;
                string currentBizName = bizName;

                while (biz is null && bizRetries < 5)
                {
                    if (bizRetries > 0)
                        currentBizName = $"{bizName} {rng.Next(100, 999)}";

                    try
                    {
                        biz = await apiClient.CreateBusinessAsync(tenant.Id, new CreateBusinessRequest(
                            currentBizName, industry, null, bizPhone,
                            bizAddress, bizCity.City, bizCity.StateAbbr, bizZip, "US", null));

                        if (biz is null) bizRetries++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  ⚠️  Warning: Business '{currentBizName}' failed: {ex.Message}");
                        break;
                    }
                }

                // Customer (person linked to the business)
                var firstName = NameData.FirstNames[rng.Next(NameData.FirstNames.Length)];
                var lastName = NameData.LastNames[rng.Next(NameData.LastNames.Length)];
                var emailSuffix = rng.Next(1000, 9999);
                var custEmail = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}.{emailSuffix}@{tenant.Slug}.customers.e2e";
                var custName = $"{firstName} {lastName}";
                var custPhone = $"(555) {rng.Next(100, 999)}-{rng.Next(1000, 9999)}";
                var companySize = companySizes[rng.Next(companySizes.Length)];

                var custCity = LocationData.Cities[rng.Next(LocationData.Cities.Length)];
                var custStreetName = WordData.StreetNames[rng.Next(WordData.StreetNames.Length)];
                var custStreetType = WordData.StreetTypes[rng.Next(WordData.StreetTypes.Length)];
                var custAddress = $"{rng.Next(1, 999)} {custStreetName} {custStreetType}";
                var custZip = $"{custCity.ZipPrefix}{rng.Next(10, 99):D2}";

                int custRetries = 0;
                CustomerResult? cust = null;

                while (cust is null && custRetries < 5)
                {
                    if (custRetries > 0)
                    {
                        emailSuffix = rng.Next(1000, 9999);
                        custEmail = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}.{emailSuffix}@{tenant.Slug}.customers.e2e";
                    }

                    try
                    {
                        cust = await apiClient.CreateCustomerAsync(tenant.Id, new CreateCustomerRequest(
                            custName, custEmail, custPhone,
                            custAddress, custCity.City, custCity.StateAbbr, custZip, "US",
                            industry, companySize, null, null, "USD",
                            biz?.Id));

                        if (cust is null) custRetries++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  ⚠️  Warning: Customer '{custName}' failed: {ex.Message}");
                        break;
                    }
                }

                if (c % 10 == 0)
                    Console.WriteLine($"    {c}/{customerCount} customers created...");
            }

            totalCustomers += customerCount;
            totalBusinesses += customerCount;
            Console.WriteLine($"  ✅ {customerCount} customers + {customerCount} businesses created");
        }

        Console.WriteLine();
        Console.WriteLine("✅ E2E seeding complete!");
        Console.WriteLine($"   Tenants created: {tenantCount}");
        Console.WriteLine($"   Employees created: {totalEmployees}");
        Console.WriteLine($"   Customers created: {totalCustomers}");
        Console.WriteLine($"   Businesses created: {totalBusinesses}");
    }
}
