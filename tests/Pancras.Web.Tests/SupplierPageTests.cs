using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Web.Tests;

public class SupplierPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public SupplierPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousDirectory_RedirectsToLogin()
    {
        using var client = _factory.CreateClient(NoRedirect);

        var response = await client.GetAsync("/suppliers");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Directory_ShowsStoredCode_AndOpensModalDialogs()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var supplier = Seed(EmployeeRole.Admin);
        await SignInAsync(client);

        var page = await client.GetAsync("/suppliers");
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("ncc01", html, StringComparison.Ordinal);
        Assert.Contains("font-mono", html, StringComparison.Ordinal);
        Assert.DoesNotContain("uppercase", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tài khoản ngân hàng", html, StringComparison.Ordinal);
        Assert.Contains("add=true", html, StringComparison.Ordinal);
        Assert.Contains("stop=" + supplier.Id.ToString("D"), html, StringComparison.Ordinal);
        Assert.Contains("Không có lô chưa nhập kho", html, StringComparison.Ordinal);
        Assert.Contains("••••3456", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCT-7788-3456", html, StringComparison.Ordinal);

        var add = await client.GetAsync("/suppliers?add=true");
        var addHtml = await add.Content.ReadAsStringAsync();
        Assert.Contains("<dialog id=\"supplier-dialog\"", addHtml, StringComparison.Ordinal);
        Assert.Contains("showModal()", addHtml, StringComparison.Ordinal);
        Assert.Contains("8–15 chữ số", addHtml, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"30\"", addHtml, StringComparison.Ordinal);
        Assert.Contains("type=\"email\"", addHtml, StringComparison.Ordinal);

        var stop = await client.GetAsync("/suppliers?stop=" + supplier.Id.ToString("D"));
        var stopHtml = await stop.Content.ReadAsStringAsync();
        Assert.Contains("<dialog id=\"supplier-stop-dialog\"", stopHtml, StringComparison.Ordinal);
        Assert.Contains("Kim An", stopHtml, StringComparison.Ordinal);

        var far = await client.GetAsync("/suppliers?edit=" + supplier.Id.ToString("D") + "&page=999");
        Assert.Equal(HttpStatusCode.Redirect, far.StatusCode);
        var location = far.Headers.Location?.OriginalString ?? "";
        Assert.Contains("edit=", location, StringComparison.Ordinal);
        Assert.DoesNotContain("page=999", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditDialog_ShowsTheFullAccountNumber_ToManagers()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var supplier = Seed(EmployeeRole.Admin);
        await SignInAsync(client);

        var edit = await client.GetAsync("/suppliers?edit=" + supplier.Id.ToString("D"));
        var html = WebUtility.HtmlDecode(await edit.Content.ReadAsStringAsync());

        Assert.Contains("<dialog id=\"supplier-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("ACCT-7788-3456", html, StringComparison.Ordinal);
        Assert.Contains("Sửa nhà cung cấp", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WarehouseKeeper_CanBrowse_ButCannotChangeAnythingOrSeeBankDetails()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var supplier = Seed(EmployeeRole.WarehouseKeeper);
        await SignInAsync(client);

        var page = await client.GetAsync("/suppliers");
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Kim An", html, StringComparison.Ordinal);
        Assert.DoesNotContain("add=true", html, StringComparison.Ordinal);
        Assert.DoesNotContain("stop=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("edit=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("••••", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCT-7788-3456", html, StringComparison.Ordinal);

        var edit = await client.GetAsync("/suppliers?edit=" + supplier.Id.ToString("D"));
        var editHtml = await edit.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<dialog", editHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCT-7788-3456", editHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InactiveFilter_ListsStoppedSuppliers_WithAReactivateButtonForAdmin()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var supplier = Seed(EmployeeRole.Admin);
        supplier.Deactivate();
        await SignInAsync(client);

        var active = await (await client.GetAsync("/suppliers")).Content.ReadAsStringAsync();
        var inactive = await (await client.GetAsync("/suppliers?status=inactive")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("ncc01", active, StringComparison.Ordinal);
        Assert.Contains("ncc01", inactive, StringComparison.Ordinal);
        Assert.Contains("Ngừng hợp tác", inactive, StringComparison.Ordinal);
        Assert.Contains("Khôi phục", inactive, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EmployeeRole.WarehouseKeeper)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead)]
    public async Task WarehouseCreateAndUpdate_GoToAccessDenied(string role)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var supplier = Seed(role);
        await SignInAsync(client);
        var page = await client.GetAsync("/suppliers");
        var token = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));

        var create = await client.PostAsync("/suppliers", Posted(token, Guid.Empty, "NCC mới"));
        var update = await client.PostAsync("/suppliers", Posted(token, supplier.Id, "Tên đã đổi"));
        var stored = _factory.Services.GetRequiredService<SupplierDirectoryDouble>().Suppliers;

        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        Assert.Contains("/access-denied", create.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);
        Assert.Contains("/access-denied", update.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var only = Assert.Single(stored);
        Assert.Equal("Kim An", only.Name);
    }

    private static FormUrlEncodedContent Posted(string token, Guid id, string name) =>
        new(new Dictionary<string, string>
        {
            ["Form.Id"] = id.ToString("D"),
            ["Form.Code"] = "ncc01",
            ["Form.Name"] = name,
            ["Form.Country"] = "Trung Quốc",
            ["__RequestVerificationToken"] = token
        });

    private Supplier Seed(string role)
    {
        var repository = _factory.Services.GetRequiredService<SupplierDirectoryDouble>();
        repository.Suppliers.Clear();
        var supplier = Supplier.Create("ncc01", "Kim An", "Trung Quốc", "An", "0901000001", "a@b.co", "ACCT-7788-3456");
        repository.Suppliers.Add(supplier);
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.Clear();
        employees.Employees.Add(Employee.Create("Người dùng thử", "admin", "ADMIN", hasher.Hash("Admin@12345"), role));
        return supplier;
    }

    private static async Task SignInAsync(HttpClient client)
    {
        var login = await client.GetAsync("/login");
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "admin",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        });
        await client.PostAsync("/login", form);
    }

    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };
}
