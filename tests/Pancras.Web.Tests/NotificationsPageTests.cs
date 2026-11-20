using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Domain.Employees;
using Pancras.Domain.Notifications;
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
    public async Task List_KeepsCreationOrder_MarksNewAndSeen_AndShowsDocumentCodes()
    {
        var inventory = _factory.Services.GetRequiredService<InventoryDirectoryDouble>();
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.RemoveAll(employee => employee.NormalizedCode == "NOTIADMIN");
        var admin = Employee.Create("notiadmin", "notiadmin", "NOTIADMIN", hasher.Hash("Admin@12345"), EmployeeRole.Admin);
        employees.Employees.Add(admin);
        var documentId = Guid.CreateVersion7();
        var seenNewest = Notification.Create(admin.Id, null, Guid.CreateVersion7(), NotificationKind.Purchase, Notification.PurchaseApprovedStep, new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero));
        seenNewest.MarkSeen();
        var unseenMiddle = Notification.Create(admin.Id, null, documentId, NotificationKind.AdjustmentPending, Notification.AdjustmentStep, new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        var unseenOldest = Notification.Create(admin.Id, null, Guid.CreateVersion7(), NotificationKind.Transfer, Notification.TransferRequestedStep, new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));
        inventory.Notices.Clear();
        inventory.Notices.AddRange([unseenOldest, seenNewest, unseenMiddle]);
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "notiadmin",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/notifications")).Content.ReadAsStringAsync());

        var newest = html.IndexOf(Notification.PurchaseApprovedStep, StringComparison.Ordinal);
        var middle = html.IndexOf(Notification.AdjustmentStep, StringComparison.Ordinal);
        var oldest = html.IndexOf(Notification.TransferRequestedStep, StringComparison.Ordinal);
        Assert.True(newest > 0 && newest < middle && middle < oldest, "Phải sắp theo thời điểm tạo, mới nhất trước, dù đã xem hay chưa.");
        Assert.Equal(2, Regex.Matches(html, @">s*Mớis*<").Count);
        Assert.Single(Regex.Matches(html, @">s*Đã xems*<"));
        Assert.Contains(IndexModel.NoticeCode(unseenMiddle)!, html, StringComparison.Ordinal);
        Assert.StartsWith("DC-", IndexModel.NoticeCode(unseenMiddle), StringComparison.Ordinal);
    }

    [Fact]
    public void NoticeCode_UsesTheSamePrefixAsTheDocumentCards()
    {
        var id = Guid.CreateVersion7();
        Notification Make(NotificationKind kind) => Notification.Create(Guid.CreateVersion7(), null, id, kind, "bước", DateTimeOffset.UtcNow);

        Assert.StartsWith("CK-", IndexModel.NoticeCode(Make(NotificationKind.Transfer)), StringComparison.Ordinal);
        Assert.StartsWith("KK-", IndexModel.NoticeCode(Make(NotificationKind.StocktakePending)), StringComparison.Ordinal);
        Assert.StartsWith("DC-", IndexModel.NoticeCode(Make(NotificationKind.AdjustmentPending)), StringComparison.Ordinal);
        Assert.StartsWith("HY-", IndexModel.NoticeCode(Make(NotificationKind.DisposalApproved)), StringComparison.Ordinal);
        Assert.Null(IndexModel.NoticeCode(Make(NotificationKind.Purchase)));
    }

    [Fact]
    public void TargetOf_OpensTheRightDocument()
    {
        var documentId = Guid.NewGuid();
        var transfer = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), documentId, NotificationKind.Transfer, Notification.TransferApprovedStep, DateTimeOffset.UtcNow);
        var adjustment = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), documentId, NotificationKind.AdjustmentPending, Notification.AdjustmentStep, DateTimeOffset.UtcNow);

        Assert.Equal($"/inventory?view=transfers&open={documentId:D}", IndexModel.TargetOf(transfer));
        Assert.Equal($"/inventory?view=stocktakes&doc={documentId:D}#document-{documentId:D}", IndexModel.TargetOf(adjustment));
        Assert.Equal("/notifications", IndexModel.TargetOf(null));
    }
}

internal sealed class NoticeStoreDouble : Pancras.Application.Notifications.INoticeStore
{
    public Task AddAsync(Notification notice, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveForDocumentAsync(Guid documentId, CancellationToken cancellationToken) => Task.CompletedTask;
}
