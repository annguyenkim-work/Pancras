using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Domain.Employees;

namespace Pancras.Web.Tests;

public class EmployeeRateLimitTests
{
    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };

    [Fact]
    public async Task AdminPosts_AreLimitedPerUser_ButBrowsingTheListIsNot()
    {
        using var factory = new PancrasWebFactory { SensitivePerMinute = "2" };
        var repository = factory.Services.GetRequiredService<FakeEmployeeRepository>();
        repository.Employees.Clear();
        repository.Employees.Add(Employee.Create("Quản trị", "admin", "ADMIN", factory.Services.GetRequiredService<FakePasswordHasher>().Hash("Admin@12345"), EmployeeRole.Admin));
        using var client = factory.CreateClient(NoRedirect);
        var login = await client.GetAsync("/login");
        var token = System.Text.RegularExpressions.Regex.Match(await login.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "admin",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));

        for (var i = 0; i < 5; i++)
        {
            var list = await client.GetAsync("/employees");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var post = await client.PostAsync("/employees?handler=Update", new FormUrlEncodedContent(new Dictionary<string, string>()));
            statuses.Add(post.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
    }
}
