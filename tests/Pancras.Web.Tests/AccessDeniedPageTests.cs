using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Domain.Employees;

namespace Pancras.Web.Tests;

public class AccessDeniedPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public AccessDeniedPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeniedPage_Returns403_WithTheStandardMessage()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.RemoveAll(employee => employee.NormalizedCode == "DENIEDUSER");
        employees.Employees.Add(Employee.Create("deniedsales", "deniedUser", "DENIEDUSER", hasher.Hash("Admin@12345"), EmployeeRole.CustomerCare));
        var login = await client.GetAsync("/login");
        var token = Regex.Match(await login.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "deniedUser",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));

        var response = await client.GetAsync("/access-denied");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Bạn không có quyền truy cập.", html, StringComparison.Ordinal);
    }
}
