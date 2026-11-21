using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Web.Tests;

/// <summary>Trang Kho &amp; Showroom giữ từ khóa đang tìm qua link và form thao tác (docs/03-ui-ux/components.md mục 10).</summary>
public class LocationsPageTests(PancrasWebFactory factory) : IClassFixture<PancrasWebFactory>
{
    [Fact]
    public async Task LinksAndActionForms_KeepTheSearchTerm()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var locations = factory.Services.GetRequiredService<LocationDirectoryDouble>();
        locations.Locations.Clear();
        locations.Locations.Add(StockLocation.Create("Kho Tìm", StockLocationKind.Warehouse));
        await SignInAsync(client);

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/locations?q=Kho")).Content.ReadAsStringAsync());

        Assert.Contains("href=\"/locations?form=1&q=Kho\"", html, StringComparison.Ordinal);
        Assert.Matches(@"href=""/locations\?edit=[0-9a-f-]+&q=Kho""", html);
        Assert.Contains("action=\"/locations?handler=Disable&q=Kho\"", html, StringComparison.Ordinal);
    }

    private async Task SignInAsync(HttpClient client)
    {
        var employees = factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.Clear();
        employees.Employees.Add(Employee.Create("Quản trị", "admin", "ADMIN", hasher.Hash("Admin@12345"), EmployeeRole.Admin));
        var login = await client.GetAsync("/login");
        var token = Regex.Match(await login.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "admin",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));
    }
}
