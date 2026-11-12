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

        var response = await client.GetAsync("/nha-cung-cap");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/dang-nhap", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Directory_ShowsStoredCode_AndOpensDialogsWithoutScript()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = _factory.Services.GetRequiredService<SupplierDirectoryDouble>();
        repository.Suppliers.Clear();
        var supplier = Supplier.Create("ncc01", "Kim An", "Trung Quốc", "An", "0901000001", "a@b.co", "123");
        repository.Suppliers.Add(supplier);
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.Clear();
        employees.Employees.Add(Employee.Create("Quản trị hệ thống", "admin", "ADMIN", hasher.Hash("Admin@12345"), EmployeeRole.Admin));
        await SignInAsync(client);

        var page = await client.GetAsync("/nha-cung-cap");
        var html = await page.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("ncc01", html, StringComparison.Ordinal);
        Assert.Contains("font-mono", html, StringComparison.Ordinal);
        Assert.DoesNotContain("uppercase", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tài khoản ngân hàng", html, StringComparison.Ordinal);
        Assert.Contains("add=true", html, StringComparison.Ordinal);
        Assert.Contains("delete=" + supplier.Id.ToString("D"), html, StringComparison.Ordinal);
        Assert.Contains("Không có lô đang về", html, StringComparison.Ordinal);

        var add = await client.GetAsync("/nha-cung-cap?add=true");
        var addHtml = await add.Content.ReadAsStringAsync();
        Assert.Contains("id=\"supplier-dialog\" class=\" fixed", addHtml, StringComparison.Ordinal);
        Assert.Contains("8–15 chữ số", addHtml, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"30\"", addHtml, StringComparison.Ordinal);

        var confirm = await client.GetAsync("/nha-cung-cap?delete=" + supplier.Id.ToString("D"));
        var confirmHtml = await confirm.Content.ReadAsStringAsync();
        Assert.Contains("id=\"delete-dialog\" class=\" fixed", confirmHtml, StringComparison.Ordinal);
        Assert.Contains("Kim An", confirmHtml, StringComparison.Ordinal);

        var far = await client.GetAsync("/nha-cung-cap?edit=" + supplier.Id.ToString("D") + "&page=999");
        Assert.Equal(HttpStatusCode.Redirect, far.StatusCode);
        var location = far.Headers.Location?.OriginalString ?? "";
        Assert.Contains("edit=", location, StringComparison.Ordinal);
        Assert.DoesNotContain("page=999", location, StringComparison.Ordinal);
    }

    private static async Task SignInAsync(HttpClient client)
    {
        var login = await client.GetAsync("/dang-nhap");
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "admin",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        });
        await client.PostAsync("/dang-nhap", form);
    }

    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };
}
