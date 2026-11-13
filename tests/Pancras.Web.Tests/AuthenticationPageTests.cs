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
    [InlineData(EmployeeRole.WarehouseKeeper, "/suppliers", true)]
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

    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };
}

public sealed class PancrasWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:LoginPerMinute", "100000");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Port=5432;Database=pancras;Username=postgres;Password=testing");
        builder.ConfigureTestServices(services =>
        {
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
        Task.FromResult<IReadOnlyList<Supplier>>(Active(null));

    public Task<SupplierSlice> ListFilteredAsync(string? term, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var ordered = Active(term);
        var size = pageSize < 1 ? SupplierPageRequest.DefaultSize : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<Supplier> items = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new SupplierSlice(items, ordered.Count, number));
    }

    public Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id && !supplier.IsDeleted));

    public Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetActiveReadOnlyAsync(id, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Suppliers.Where(supplier => ids.Contains(supplier.Id)).ToDictionary(supplier => supplier.Id, supplier => supplier.Name));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Any(supplier =>
            !supplier.IsDeleted && supplier.NormalizedCode == normalizedCode && supplier.Id != excludingId));

    public Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Count(supplier =>
            !supplier.IsDeleted
            && (string.CompareOrdinal(supplier.Name, name) < 0
                || (supplier.Name == name && string.CompareOrdinal(supplier.Code, code) < 0))));

    public Task<bool> TryAddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        Suppliers.Add(supplier);
        return Task.FromResult(true);
    }

    public Task<bool> TrySaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    private List<Supplier> Active(string? term)
    {
        IEnumerable<Supplier> suppliers = Suppliers.Where(supplier => !supplier.IsDeleted);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var needle = term.Trim();
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
    public Task RecordAsync(Pancras.Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<Pancras.Domain.Auditing.AuditEntry>> ListAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Pancras.Domain.Auditing.AuditEntry>>([]);

    public Task<int> CountAsync(string? search, CancellationToken cancellationToken) => Task.FromResult(0);
}
