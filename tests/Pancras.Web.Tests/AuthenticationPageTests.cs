using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Application.Purchasing;
using Pancras.Application.Inventory;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Web.Tests;

public class AuthenticationPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;
    private readonly FakePasswordHasher _hasher = new();

    public AuthenticationPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders_AndNoCdnScript()
    {
        using var client = _factory.CreateClient(NoRedirect);

        var response = await client.GetAsync("/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.tailwindcss.com", html, StringComparison.Ordinal);
        Assert.Contains("/css/site.css", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousHome_RedirectsToLogin()
    {
        using var client = _factory.CreateClient(NoRedirect);
        ResetEmployees();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_SetsPersistentCookie_AndCreatePageDoesNotDefaultToAdmin()
    {
        using var client = _factory.CreateClient(NoRedirect);
        ResetEmployees(Admin());

        var response = await SignInAsync(client, "admin", "Admin@12345");
        var setCookie = string.Join("\n", response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("Pancras.Auth", setCookie, StringComparison.Ordinal);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);

        var create = await client.GetAsync("/employees?form=1");
        var html = await create.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.Contains("Chọn vai trò", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"Admin\" selected", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sales_CannotOpenCreateEmployee()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var sales = Employee.Create("Lê Thị Sales", "SalesLT", "SALESLT", _hasher.Hash("Secret1"), EmployeeRole.Sales);
        ResetEmployees(sales);
        await SignInAsync(client, "SalesLT", "Secret1");

        var response = await client.GetAsync("/employees?form=1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/access-denied", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EmployeeRole.Sales, "/suppliers", true)]
    [InlineData(EmployeeRole.WarehouseKeeper, "/suppliers", false)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead, "/suppliers", false)]
    [InlineData(EmployeeRole.Accountant, "/suppliers", false)]
    [InlineData(EmployeeRole.Sales, "/employees", true)]
    [InlineData(EmployeeRole.CustomerCare, "/employees", true)]
    [InlineData(EmployeeRole.Accountant, "/employees", false)]
    [InlineData(EmployeeRole.Sales, "/locations", true)]
    [InlineData(EmployeeRole.Accountant, "/locations", false)]
    [InlineData(EmployeeRole.Sales, "/profile", false)]
    public async Task RolesSeeOnlyTheirPages(string role, string path, bool denied)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var person = Employee.Create("Lê Thị Test", "TestLT", "TESTLT", _hasher.Hash("Secret12"), role);
        ResetEmployees(person);
        await SignInAsync(client, "TestLT", "Secret12");

        var response = await client.GetAsync(path);

        if (denied)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/access-denied", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task DeactivatedEmployee_CannotKeepUsingTheSession()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var person = Employee.Create("Lê Thị Test", "TestLT", "TESTLT", _hasher.Hash("Secret12"), EmployeeRole.Accountant);
        ResetEmployees(person);
        await SignInAsync(client, "TestLT", "Secret12");
        person.Deactivate();

        var response = await client.GetAsync("/suppliers");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangedPassword_EndsTheExistingSession()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var admin = Admin();
        ResetEmployees(admin);
        await SignInAsync(client, "admin", "Admin@12345");
        Assert.True(admin.ReplacePassword(_hasher.Hash("other-password")));

        var response = await client.GetAsync("/employees?form=1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelTwoFactor_ClearsThePendingSession()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var person = Employee.Create("Lê Thị Hai", "TwoFaLT", "TWOFALT", _hasher.Hash("Secret12"), EmployeeRole.Accountant);
        person.EnableTwoFactor("protected-secret", [], 0);
        ResetEmployees(person);

        var login = await SignInAsync(client, "TwoFaLT", "Secret12");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("/login/verify", login.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var page = await client.GetAsync("/login/verify");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("handler=Cancel", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/login\"", html, StringComparison.Ordinal);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));

        var cancel = await client.PostAsync("/login/verify?handler=Cancel", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Contains("/login", cancel.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("/login/verify", cancel.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var again = await client.GetAsync("/login/verify");
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Contains("/login", again.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownUrl_WhenSignedIn_ShowsVietnameseNotFoundPage()
    {
        using var client = _factory.CreateClient(NoRedirect);
        ResetEmployees(Admin());
        await SignInAsync(client, "admin", "Admin@12345");

        var response = await client.GetAsync("/abc");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Không tìm thấy trang", html, StringComparison.Ordinal);
        Assert.Contains("Về trang chủ", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasswordHints_StateTheEightToOneHundredTwentyEightRange()
    {
        using var client = _factory.CreateClient(NoRedirect);
        ResetEmployees(Admin());
        await SignInAsync(client, "admin", "Admin@12345");

        var profile = await client.GetAsync("/profile");
        var employees = await client.GetAsync("/employees?form=1");
        var profileHtml = await profile.Content.ReadAsStringAsync();
        var employeesHtml = await employees.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal(HttpStatusCode.OK, employees.StatusCode);
        Assert.Contains("8–128 ký tự", profileHtml, StringComparison.Ordinal);
        Assert.Contains("8–128 ký tự", employeesHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("từ 8 ký tự", profileHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("từ 8 ký tự", employeesHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginRateLimit_ReturnsVietnameseTooManyRequestsPage()
    {
        using var factory = new PancrasWebFactory { LoginPerMinute = "2" };
        using var client = factory.CreateClient(NoRedirect);

        var first = await client.GetAsync("/login");
        var second = await client.GetAsync("/login");
        var third = await client.GetAsync("/login");
        var html = await third.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Contains("Bạn thử đăng nhập quá nhiều lần. Vui lòng đợi một phút rồi thử lại.", html, StringComparison.Ordinal);
        Assert.Contains("Về trang chủ", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Session_EndsTwelveHoursAfterSignIn_EvenIfTheCookieSlides()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var factory = new PancrasWebFactory { Clock = clock };
        using var client = factory.CreateClient(NoRedirect);
        var repository = factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = factory.Services.GetRequiredService<FakePasswordHasher>();
        repository.Employees.Add(Employee.Create(
            "Lê Thị Test", "TestLT", "TESTLT", hasher.Hash("Secret12"), EmployeeRole.Accountant));

        var login = await SignInAsync(client, "TestLT", "Secret12");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        clock.Advance(TimeSpan.FromHours(5));
        var stillSignedIn = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);

        clock.Advance(TimeSpan.FromHours(6));
        var pastSlidingWindow = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, pastSlidingWindow.StatusCode);

        clock.Advance(TimeSpan.FromHours(1));
        var expired = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, expired.StatusCode);
        Assert.Contains("/login", expired.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateWithoutRole_SaysARoleIsRequired_AndCreatesNobody()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        ResetEmployees(Admin());
        await SignInAsync(client, "admin", "Admin@12345");
        var before = repository.Employees.Count;

        var response = await PostCreateAsync(client, "");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cần chọn vai trò.", html, StringComparison.Ordinal);
        Assert.Contains("disabled", html, StringComparison.Ordinal);
        Assert.Equal(before, repository.Employees.Count);
    }

    [Fact]
    public async Task AccountantCreate_IsSentToAccessDenied()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var accountant = Employee.Create("Kế Toán", "KetoanKT", "KETOANKT", _hasher.Hash("Secret12"), EmployeeRole.Accountant);
        ResetEmployees(accountant);
        await SignInAsync(client, "KetoanKT", "Secret12");

        var response = await PostCreateAsync(client, EmployeeRole.Sales);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/access-denied", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Single(repository.Employees);
    }

    [Fact]
    public async Task ActiveEmployee_ShowsStop_AndOwnRowHasNoEdit()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var admin = Admin();
        var other = Employee.Create("Nguyễn Kim An", "AnNK", "ANNK", _hasher.Hash("Secret12"), EmployeeRole.Accountant);
        ResetEmployees(admin, other);
        await SignInAsync(client, "admin", "Admin@12345");

        var response = await client.GetAsync("/employees");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ngừng hoạt động nhân viên này?", html, StringComparison.Ordinal);
        Assert.Contains(">Ngừng<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Kích hoạt lại<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Xóa<", html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, ">Sửa<"));

        other.Deactivate();
        var stopped = await client.GetAsync("/employees");
        var stoppedHtml = WebUtility.HtmlDecode(await stopped.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        Assert.Contains(">Kích hoạt lại<", stoppedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(">Ngừng<", stoppedHtml, StringComparison.Ordinal);
    }

    private Employee Admin() =>
        Employee.Create("Quản trị hệ thống", "admin", "ADMIN", _hasher.Hash("Admin@12345"), EmployeeRole.Admin);

    private void ResetEmployees(params Employee[] employees)
    {
        var repository = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        repository.Employees.Clear();
        repository.Employees.AddRange(employees);
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string code, string password)
    {
        var login = await client.GetAsync("/login");
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = code,
            ["Command.Password"] = password,
            ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync("/login", form);
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(HttpClient client, string role)
    {
        var page = await client.GetAsync("/employees?form=1");
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.FullName"] = "Nguyễn Văn Mới",
            ["Command.Email"] = "moi@example.com",
            ["Command.Phone"] = "0901000001",
            ["Command.JobTitle"] = "Nhân viên",
            ["Command.Password"] = "Secret123",
            ["Command.Role"] = role,
            ["__RequestVerificationToken"] = token
        });
        return await client.PostAsync("/employees?handler=Create", form);
    }

    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };
}

public sealed class PancrasWebFactory : WebApplicationFactory<Program>
{
    public string LoginPerMinute { get; init; } = "100000";

    public TimeProvider? Clock { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:LoginPerMinute", LoginPerMinute);
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Port=5432;Database=pancras;Username=postgres;Password=testing");
        builder.ConfigureTestServices(services =>
        {
            if (Clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(Clock);
            }

            services.RemoveAll<Pancras.Application.Auditing.IAuditLog>();
            services.AddSingleton<Pancras.Application.Auditing.IAuditLog, NoAuditLog>();
            services.RemoveAll<IEmployeeRepository>();
            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<FakePasswordHasher>();
            services.AddSingleton<IPasswordHasher>(provider => provider.GetRequiredService<FakePasswordHasher>());
            services.AddSingleton<FakeEmployeeRepository>();
            services.AddSingleton<IEmployeeRepository>(provider => provider.GetRequiredService<FakeEmployeeRepository>());
            services.RemoveAll<Pancras.Application.Locations.ILocationRepository>();
            services.AddSingleton<LocationDirectoryDouble>();
            services.AddSingleton<Pancras.Application.Locations.ILocationRepository>(provider => provider.GetRequiredService<LocationDirectoryDouble>());
            services.RemoveAll<ISupplierRepository>();
            services.AddSingleton<SupplierDirectoryDouble>();
            services.AddSingleton<ISupplierRepository>(provider => provider.GetRequiredService<SupplierDirectoryDouble>());
            services.RemoveAll<ICatalogItemRepository>();
            services.AddSingleton<CatalogDirectoryDouble>();
            services.AddSingleton<ICatalogItemRepository>(provider => provider.GetRequiredService<CatalogDirectoryDouble>());
            services.RemoveAll<IInventoryRepository>();
            services.AddSingleton<InventoryDirectoryDouble>();
            services.AddSingleton<IInventoryRepository>(provider => provider.GetRequiredService<InventoryDirectoryDouble>());
            services.RemoveAll<Pancras.Application.Inventory.IStockDocumentAttachmentRepository>();
            services.AddSingleton<Pancras.Application.Inventory.IStockDocumentAttachmentRepository, StockDocumentAttachmentDouble>();
            services.RemoveAll<Pancras.Application.Inventory.ITransferAttachmentRepository>();
            services.AddSingleton<Pancras.Application.Inventory.ITransferAttachmentRepository, TransferAttachmentDouble>();
            services.RemoveAll<IPurchaseOrderRepository>();
            services.AddSingleton<PurchaseDirectoryDouble>();
            services.AddSingleton<IPurchaseOrderRepository>(provider => provider.GetRequiredService<PurchaseDirectoryDouble>());
            services.RemoveAll<ISupplierPurchaseStatsReader>();
            services.AddSingleton<SupplierStatsDouble>();
            services.AddSingleton<ISupplierPurchaseStatsReader>(provider => provider.GetRequiredService<SupplierStatsDouble>());
            services.RemoveAll<IPurchaseAttachmentRepository>();
            services.RemoveAll<IAttachmentStorage>();
            services.AddSingleton<AttachmentDouble>();
            services.AddSingleton<IPurchaseAttachmentRepository>(provider => provider.GetRequiredService<AttachmentDouble>());
            services.AddSingleton<IAttachmentStorage>(provider => provider.GetRequiredService<AttachmentDouble>());
        });
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _utc = start;

    public void Advance(TimeSpan by) => _utc += by;

    public override DateTimeOffset GetUtcNow() => _utc;
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hash:" + password;
}

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    public List<Employee> Employees { get; } = [];

    public Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(employee => employee.NormalizedCode == normalizedCode));

    public Task<Employee?> GetByNormalizedCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken) =>
        GetByNormalizedCodeAsync(normalizedCode, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.Any(employee => employee.NormalizedCode == normalizedCode));

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(employee => employee.Id == id));

    public Task<Employee?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Employee>> ListReadOnlyAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Employee>>(Employees.ToArray());

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Employees.Count(employee => employee.Role == EmployeeRole.Admin && employee.IsActive));

    public Task<bool> ActiveLeadExistsAsync(Guid stockLocationId, Guid? excludeEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task AddAsync(Employee employee, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<T> WithinPersonnelRulesAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) =>
        action(cancellationToken);
}

internal sealed class SupplierStatsDouble : ISupplierPurchaseStatsReader
{
    public Dictionary<Guid, SupplierPurchaseStats> Stats { get; } = [];

    public Task<IReadOnlyDictionary<Guid, SupplierPurchaseStats>> ReadAsync(IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, SupplierPurchaseStats>>(Stats);
}

internal sealed class SupplierDirectoryDouble : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Supplier>>(Filtered(SupplierListFilter.From(null)));

    public Task<SupplierSlice> ListFilteredAsync(SupplierListFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var ordered = Filtered(filter);
        var size = pageSize < 1 ? SupplierPageRequest.DefaultSize : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<Supplier> items = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new SupplierSlice(items, ordered.Count, number));
    }

    public Task<IReadOnlyList<string>> ListCountriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Suppliers.Select(supplier => supplier.Country).Distinct().Order().ToList());

    public Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id && !supplier.IsInactive));

    public Task<Supplier?> GetAnyReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id));

    public Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetActiveReadOnlyAsync(id, cancellationToken);

    public Task<Supplier?> GetAnyForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetAnyReadOnlyAsync(id, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Suppliers.Where(supplier => ids.Contains(supplier.Id)).ToDictionary(supplier => supplier.Id, supplier => supplier.Name));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Any(supplier =>
            !supplier.IsInactive && supplier.NormalizedCode == normalizedCode && supplier.Id != excludingId));

    public Task<string?> InactiveNameWithCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.IsInactive && supplier.NormalizedCode == normalizedCode)?.Name);

    public Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Count(supplier =>
            !supplier.IsInactive
            && (string.CompareOrdinal(supplier.Name, name) < 0
                || (supplier.Name == name && string.CompareOrdinal(supplier.Code, code) < 0))));

    public Task<bool> TryAddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        Suppliers.Add(supplier);
        return Task.FromResult(true);
    }

    public Task<bool> TrySaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    private List<Supplier> Filtered(SupplierListFilter filter)
    {
        IEnumerable<Supplier> suppliers = filter.Status switch
        {
            SupplierStatusFilter.Inactive => Suppliers.Where(supplier => supplier.IsInactive),
            SupplierStatusFilter.All => Suppliers,
            _ => Suppliers.Where(supplier => !supplier.IsInactive)
        };
        if (filter.Country is not null)
        {
            suppliers = suppliers.Where(supplier => supplier.Country == filter.Country);
        }

        if (filter.Term is not null)
        {
            var needle = filter.Term;
            suppliers = suppliers.Where(supplier =>
                supplier.Code.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        return suppliers.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Code).ToList();
    }
}

internal sealed class LocationDirectoryDouble : Pancras.Application.Locations.ILocationRepository
{
    public List<Pancras.Domain.Inventory.StockLocation> Locations { get; } = [];

    public Task<IReadOnlyList<Pancras.Domain.Inventory.StockLocation>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Pancras.Domain.Inventory.StockLocation>>(Locations.ToArray());

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Locations.Any(location => location.Id == id));

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Locations.Any(location => string.Equals(location.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> TryAddAsync(Pancras.Domain.Inventory.StockLocation location, CancellationToken cancellationToken)
    {
        Locations.Add(location);
        return Task.FromResult(true);
    }
}

internal sealed class NoAuditLog : Pancras.Application.Auditing.IAuditLog
{
    public List<Pancras.Domain.Auditing.AuditEntry> Entries { get; } = [];

    public Task RecordAsync(Pancras.Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (Entries)
        {
            Entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Pancras.Domain.Auditing.AuditEntry>> ListAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Pancras.Domain.Auditing.AuditEntry>>([]);

    public Task<int> CountAsync(string? search, CancellationToken cancellationToken) => Task.FromResult(0);
}
