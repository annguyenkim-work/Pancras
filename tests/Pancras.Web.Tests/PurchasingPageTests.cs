using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Purchasing;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Web.Tests;

public class PurchasingPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public PurchasingPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousPurchasing_RedirectsToLogin()
    {
        using var client = _factory.CreateClient(NoRedirect);

        var response = await client.GetAsync("/purchasing");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyPurchasing_ShowsTheEmptySentence()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client);

        var page = await client.GetAsync("/purchasing");
        var text = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Chưa có đơn mua.", text, StringComparison.Ordinal);
        Assert.Contains("Mua hàng & Container Nhập", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReceivedOrder_HidesTheReceiveButton()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = PurchaseOrder.TryCreate(
            "PO-DA-NHAP",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "CONT",
            "BL",
            "Hải Phòng",
            new DateOnly(2026, 11, 12),
            25000m,
            0.10m,
            [(Guid.CreateVersion7(), 1, 10m, 0.05m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        Assert.Equal(ReceiptDecision.Ok, order.TryRecordReceipt(order.Lines.ToDictionary(line => line.Id, line => line.Quantity), true, null, DateTimeOffset.UtcNow, "mua", out _, out _));
        orders.Orders.Add(order);
        await SignInAsync(client);

        var page = await client.GetAsync("/purchasing?order=" + order.Id);
        var text = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("PO-DA-NHAP", text, StringComparison.Ordinal);
        Assert.Contains("Đã nhập kho", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Nhập kho", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Chưa có đơn mua.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCreate_KeepsTheEnteredLines()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client);
        var formPage = await client.GetAsync("/purchasing?form=1");
        var html = await formPage.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var itemId = Guid.CreateVersion7();

        var response = await client.PostAsync("/purchasing?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.Code"] = "PO-GIU",
            ["Form.ExchangeRate"] = "25000",
            ["Form.VatPercent"] = "10",
            ["Form.OceanFreight"] = "12000000",
            ["Form.Lines[0].CatalogItemId"] = itemId.ToString(),
            ["Form.Lines[0].Quantity"] = "60",
            ["Form.UnitPrices[0]"] = "520",
            ["Form.DutyPercents[0]"] = "15"
        }));
        var text = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Đơn không được tạo.", text, StringComparison.Ordinal);
        Assert.Contains("PO-GIU", text, StringComparison.Ordinal);
        Assert.Contains("value=\"60\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"520\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"15\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"Form.Lines[0].CatalogItemId\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheForm_PutsItemsBeforeTheOrderWideCosts_AndLocksTheCostsUntilThereAreItems()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client);

        var html = await (await client.GetAsync("/purchasing?form=1")).Content.ReadAsStringAsync();

        var items = html.IndexOf("id=\"line-body\"", StringComparison.Ordinal);
        var costs = html.IndexOf("id=\"charge-body\"", StringComparison.Ordinal);
        Assert.True(items > 0 && costs > items, "Mặt hàng phải nằm trước chi phí chung.");
        Assert.Contains("id=\"charge-area\"", html, StringComparison.Ordinal);
        Assert.Contains("Nhập mặt hàng, số lượng và đơn giá FOB", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectedSave_ShowsTheReasonInsideTheOpenDialog_NotBehindIt()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client);
        var formPage = await client.GetAsync("/purchasing?form=1");
        var token = Regex.Match(await formPage.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/purchasing?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.Code"] = "PO-GIA-0",
            ["Form.ExchangeRate"] = "25000",
            ["Form.Lines[0].CatalogItemId"] = Guid.CreateVersion7().ToString(),
            ["Form.Lines[0].Quantity"] = "5",
            ["Form.UnitPrices[0]"] = "0"
        }));
        var text = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var dialogStart = text.IndexOf("<dialog", StringComparison.Ordinal);
        var dialogEnd = text.IndexOf("</dialog>", StringComparison.Ordinal);
        const string reason = "Đơn không được tạo.";

        Assert.True(dialogStart > 0 && dialogEnd > dialogStart);
        Assert.Contains(reason, text[dialogStart..dialogEnd], StringComparison.Ordinal);
        Assert.DoesNotContain(reason, text[..dialogStart], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Attachment_CanBeUploadedAndDownloaded_ByFinanceRoles()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var (order, files) = SeedOrder();
        await SignInAsync(client);
        var token = await TokenAsync(client, order.Id);
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x31, 0x32, 0x33 };

        var upload = await client.PostAsync("/purchasing?handler=Upload", Multipart(token, order.Id, "to-khai.pdf", pdf));
        var stored = Assert.Single(files.Items);
        var download = await client.GetAsync("/purchasing?handler=Download&id=" + stored.Id);

        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        Assert.Contains("notice=attachment-added", upload.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("to-khai.pdf", download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(files.Files.Keys, name => name.Contains("to-khai", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Attachment_WithWrongContent_IsRejected()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var (order, files) = SeedOrder();
        await SignInAsync(client);
        var token = await TokenAsync(client, order.Id);

        var response = await client.PostAsync("/purchasing?handler=Upload", Multipart(token, order.Id, "virus.pdf", "MZ-not-a-pdf"u8.ToArray()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=AttachmentInvalid", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Empty(files.Items);
        Assert.Empty(files.Files);
    }

    [Fact]
    public async Task Attachment_IsNotDownloadable_ByWarehouseStaff()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var (order, files) = SeedOrder();
        var stored = PurchaseAttachment.TryCreate(order.Id, AttachmentKind.Other, "mat.pdf", "0123456789abcdef0123456789abcdef.pdf", 5, DateTimeOffset.UtcNow, "x")!;
        files.Items.Add(stored);
        files.Files[stored.StoredName] = "%PDF-"u8.ToArray();
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.Add(Employee.Create("kho", "kho", "KHO", hasher.Hash("Admin@12345"), EmployeeRole.WarehouseKeeper));
        var login = await client.GetAsync("/login");
        var token = Regex.Match(await login.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "kho",
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));

        var response = await client.GetAsync("/purchasing?handler=Download&id=" + stored.Id);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private (PurchaseOrder Order, AttachmentDouble Files) SeedOrder()
    {
        var orders = Reset();
        var files = _factory.Services.GetRequiredService<AttachmentDouble>();
        files.Items.Clear();
        files.Files.Clear();
        var order = PurchaseOrder.TryCreate(
            "PO-CT",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "CONT",
            "BL",
            "Hải Phòng",
            null,
            25000m,
            0.10m,
            [(Guid.CreateVersion7(), 1, 10m, 0.05m, 0m, 0m, 0m)])!;
        orders.Orders.Add(order);
        return (order, files);
    }

    private static async Task<string> TokenAsync(HttpClient client, Guid orderId)
    {
        var page = await client.GetAsync("/purchasing?order=" + orderId);
        return Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private static MultipartFormDataContent Multipart(string token, Guid orderId, string fileName, byte[] bytes)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(orderId.ToString()), "orderId" },
            { new StringContent("1"), "kind" }
        };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return form;
    }

    private static PurchaseOrder NewOrder(string code, Guid receiveLocation)
    {
        var order = PurchaseOrder.TryCreate(
            code,
            Guid.CreateVersion7(),
            receiveLocation,
            "CONT",
            "BL",
            "Hải Phòng",
            new DateOnly(2026, 11, 12),
            25000m,
            0.10m,
            [(Guid.CreateVersion7(), 4, 10m, 0.05m, 0m, 0m, 0m)])!;
        return order;
    }

    [Fact]
    public async Task WarehouseKeeper_OpensThePage_SeesOnlyApprovedOrdersOfTheirOwnLocation_WithoutAnyPrice()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var mine = Guid.CreateVersion7();
        var approvedMine = NewOrder("PO-KHO-CUA-TOI", mine);
        Assert.True(approvedMine.TrySubmit());
        Assert.True(approvedMine.TryApprove());
        var draftMine = NewOrder("PO-NHAP-CUA-TOI", mine);
        var approvedElsewhere = NewOrder("PO-KHO-KHAC", Guid.CreateVersion7());
        Assert.True(approvedElsewhere.TrySubmit());
        Assert.True(approvedElsewhere.TryApprove());
        orders.Orders.AddRange([approvedMine, draftMine, approvedElsewhere]);
        await SignInAsync(client, EmployeeRole.WarehouseKeeper, mine);

        var page = await client.GetAsync("/purchasing?order=" + approvedMine.Id);
        var text = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("PO-KHO-CUA-TOI", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PO-NHAP-CUA-TOI", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PO-KHO-KHAC", text, StringComparison.Ordinal);
        Assert.Contains("Nhập Kho & Cập Nhật Giá Vốn", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Giá FOB", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tỷ giá", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tạo Đơn Mua Hàng", text, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"order-workflow\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sửa đơn", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WarehouseKeeper_CannotOpenTheCreateOrEditForm_NorStartAWorkflowStep()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var mine = Guid.CreateVersion7();
        var order = NewOrder("PO-KHO", mine);
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        orders.Orders.Add(order);
        await SignInAsync(client, EmployeeRole.WarehouseKeeper, mine);

        var formPage = await client.GetAsync("/purchasing?form=1");
        var editPage = await client.GetAsync("/purchasing?edit=" + order.Id);
        var html = await formPage.Content.ReadAsStringAsync() + await editPage.Content.ReadAsStringAsync();

        Assert.DoesNotContain("id=\"purchase-draft\"", html, StringComparison.Ordinal);
        Assert.Equal(PurchaseStatus.Approved, order.Status);
    }

    [Fact]
    public async Task Accountant_SeesTheWorkflowButtonsOfTheCurrentStatus()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var draft = NewOrder("PO-NHAP", Guid.CreateVersion7());
        var pending = NewOrder("PO-CHO-DUYET", Guid.CreateVersion7());
        Assert.True(pending.TrySubmit());
        orders.Orders.AddRange([draft, pending]);
        await SignInAsync(client, EmployeeRole.Accountant);

        var draftText = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + draft.Id)).Content.ReadAsStringAsync());
        var pendingText = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + pending.Id)).Content.ReadAsStringAsync());

        Assert.Contains("Gửi duyệt", draftText, StringComparison.Ordinal);
        Assert.DoesNotContain("Duyệt đơn", draftText, StringComparison.Ordinal);
        Assert.Contains("Duyệt đơn", pendingText, StringComparison.Ordinal);
        Assert.Contains("Trả về nháp", pendingText, StringComparison.Ordinal);
        Assert.Contains("Hủy đơn", pendingText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmittingFromThePage_MovesTheOrderToPendingApproval()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-GUI", Guid.CreateVersion7());
        orders.Orders.Add(order);
        await SignInAsync(client, EmployeeRole.Accountant);
        var html = await (await client.GetAsync("/purchasing?order=" + order.Id)).Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/purchasing?handler=ChangeStatus", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["orderId"] = order.Id.ToString(),
            ["action"] = ((int)PurchaseStatusAction.Submit).ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("notice=submitted", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Equal(PurchaseStatus.PendingApproval, order.Status);
    }

    private PurchaseDirectoryDouble Reset()
    {
        var orders = _factory.Services.GetRequiredService<PurchaseDirectoryDouble>();
        orders.Orders.Clear();
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        employees.Employees.Clear();
        return orders;
    }

    private async Task SignInAsync(HttpClient client, string role = EmployeeRole.Admin, Guid? locationId = null)
    {
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.Add(Employee.Create("mua", "mua", "MUA", hasher.Hash("Admin@12345"), role, stockLocationId: locationId));
        var login = await client.GetAsync("/login");
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = "mua",
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

internal sealed class AttachmentDouble : IPurchaseAttachmentRepository, IAttachmentStorage
{
    public List<PurchaseAttachment> Items { get; } = [];

    public Dictionary<string, byte[]> Files { get; } = [];

    public Task<IReadOnlyList<PurchaseAttachment>> ListAsync(Guid purchaseOrderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseAttachment>>(Items.Where(item => item.PurchaseOrderId == purchaseOrderId).ToArray());

    public Task<PurchaseAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task AddAsync(PurchaseAttachment attachment, CancellationToken cancellationToken)
    {
        Items.Add(attachment);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        Items.RemoveAll(item => item.Id == id);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        Files[storedName] = copy.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Files.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
    {
        Files.Remove(storedName);
        return Task.CompletedTask;
    }
}

internal sealed class PurchaseDirectoryDouble : IPurchaseOrderRepository
{
    public List<PurchaseOrder> Orders { get; } = [];

    public Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseOrder>>(Orders.ToArray());

    public Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.FirstOrDefault(order => order.Id == id));

    public Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindReadOnlyAsync(id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.Any(order => order.NormalizedCode == normalizedCode));

    public Task<bool> TryAddAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        Orders.Add(order);
        return Task.FromResult(true);
    }

    public Task<bool> CommitAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
