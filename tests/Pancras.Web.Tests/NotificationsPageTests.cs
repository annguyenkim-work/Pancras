using Pancras.Domain.Notifications;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Web.Pages.Notifications;

namespace Pancras.Web.Tests;

public class NotificationsPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public NotificationsPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_IsRedirectedToLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/notifications");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedIn_SeesTheBellOnEveryPage_AndAnEmptyList()
    {
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.Clear();
        employees.Employees.Add(Employee.Create("Người dùng thử", "admin", "ADMIN", hasher.Hash("Admin@12345"), EmployeeRole.Admin));
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "admin",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));

        var page = await client.GetAsync("/notifications");
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Chưa có thông báo nào", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/notifications\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetOf_OpensTheRightDocument()
    {
        var documentId = Guid.NewGuid();
        var transfer = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), documentId, NotificationKind.Transfer, Notification.TransferApprovedStep, DateTimeOffset.UtcNow);
        var adjustment = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), documentId, NotificationKind.AdjustmentPending, Notification.AdjustmentStep, DateTimeOffset.UtcNow);

        Assert.Equal($"/inventory?view=transfers&open={documentId:D}", IndexModel.TargetOf(transfer));
        Assert.Equal($"/inventory?view=stocktakes#document-{documentId:D}", IndexModel.TargetOf(adjustment));
        Assert.Equal("/notifications", IndexModel.TargetOf(null));
    }
}

internal sealed class NoticeStoreDouble : Pancras.Application.Notifications.INoticeStore
{
    public Task AddAsync(Notification notice, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveForDocumentAsync(Guid documentId, CancellationToken cancellationToken) => Task.CompletedTask;
}
