using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Domain.Employees;

namespace Pancras.Web.Tests;

public class AuditPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public AuditPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };

    private async Task SignInAdminAsync(HttpClient client)
    {
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.RemoveAll(employee => employee.NormalizedCode == "AUDITADMIN");
        employees.Employees.Add(Employee.Create("auditadmin", "auditadmin", "AUDITADMIN", hasher.Hash("Admin@12345"), EmployeeRole.Admin));
        var login = await client.GetAsync("/login");
        var token = Regex.Match(await login.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "auditadmin",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));
    }

    [Fact]
    public async Task AuditList_UsesKeysetNavigation_NotNumberedPages()
    {
        using var client = _factory.CreateClient(NoRedirect);
        await SignInAdminAsync(client);

        var response = await client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("pageNumber=", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/audit?after=khong-phai-dau-trang")]
    [InlineData("/audit?before=%00%00")]
    [InlineData("/audit?pageNumber=3&size=50")]
    public async Task AuditList_IgnoresBrokenOrOldPagingParameters(string url)
    {
        using var client = _factory.CreateClient(NoRedirect);
        await SignInAdminAsync(client);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
