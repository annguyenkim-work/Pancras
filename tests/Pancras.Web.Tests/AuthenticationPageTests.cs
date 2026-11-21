using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Application.Inventory;
using Pancras.Application.Purchasing;
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
        Assert.DoesNotContain("unsafe-inline", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
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
    public async Task ProfileRateLimit_LimitsPasswordAndTwoFactorActions()
    {
        using var factory = new PancrasWebFactory { SensitivePerMinute = "2" };
        var repository = factory.Services.GetRequiredService<FakeEmployeeRepository>();
        repository.Employees.Clear();
        repository.Employees.Add(Employee.Create("Quản trị hệ thống", "admin", "ADMIN", factory.Services.GetRequiredService<FakePasswordHasher>().Hash("Admin@12345"), EmployeeRole.Admin));
        using var client = factory.CreateClient(NoRedirect);
        await SignInAsync(client, "admin", "Admin@12345");

        var first = await client.GetAsync("/profile");
        var second = await client.GetAsync("/profile");
        var third = await client.GetAsync("/profile");
        var html = await third.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Contains("Bạn thao tác quá nhiều lần", html, StringComparison.Ordinal);
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
    public async Task CreatingAnEmployee_ReturnsToTheSameFilteredList()
    {
        // Rà toàn bộ (2026-11-21): tạo nhân viên thành công thì quay về danh sách không còn bộ lọc, vì trang chỉ nhớ bộ lọc khi lưu lỗi.
        using var client = _factory.CreateClient(NoRedirect);
        ResetEmployees(Admin());
        await SignInAsync(client, "admin", "Admin@12345");
        var page = await client.GetAsync("/employees?form=1&q=Nguy&role=" + EmployeeRole.Sales + "&size=50");
        var token = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/employees?handler=Create&q=Nguy&role=" + EmployeeRole.Sales + "&size=50", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.FullName"] = "Nguyễn Văn Lọc",
            ["Command.Email"] = "loc@example.com",
            ["Command.Phone"] = "0901000002",
            ["Command.JobTitle"] = "Nhân viên",
            ["Command.Password"] = "Secret123",
            ["Command.Role"] = EmployeeRole.Accountant,
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains("q=Nguy", location, StringComparison.Ordinal);
        Assert.Contains("role=" + EmployeeRole.Sales, location, StringComparison.Ordinal);
        Assert.Contains("size=50", location, StringComparison.Ordinal);
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
