using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Catalog;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Notifications;

using Pancras.Tests.Shared;

namespace Pancras.Web.Tests;

public class InventoryPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public InventoryPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousInventory_RedirectsToLogin()
    {
        using var client = _factory.CreateClient(NoRedirect);

        var response = await client.GetAsync("/inventory");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inventory_ShowsTheFourQuantityLabels()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, EmployeeRole.WarehouseKeeper, "kho");

        var page = await client.GetAsync("/inventory");
        var text = Decode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Tồn thực tế", text, StringComparison.Ordinal);
        Assert.Contains("Đang giữ chỗ", text, StringComparison.Ordinal);
        Assert.Contains("role=\"tablist\"", text, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-transfers\" href=\"/inventory?view=transfers\"", text, StringComparison.Ordinal);
        Assert.Contains("Khả dụng", text, StringComparison.Ordinal);
        Assert.Contains("Đang chuyển", text, StringComparison.Ordinal);
        Assert.Contains("Quản Lý Đa Kho", text, StringComparison.Ordinal);
        Assert.Contains("Không có kết quả.", text, StringComparison.Ordinal);

        var transfers = Decode(await (await client.GetAsync("/inventory?view=transfers")).Content.ReadAsStringAsync());
        Assert.Contains("Lịch Sử Điều Chuyển", transfers, StringComparison.Ordinal);
        Assert.Contains("Chưa có phiếu chuyển.", transfers, StringComparison.Ordinal);

        var stocktakes = Decode(await (await client.GetAsync("/inventory?view=stocktakes")).Content.ReadAsStringAsync());
        Assert.Contains("Kiểm Kê Kho Định Kỳ", stocktakes, StringComparison.Ordinal);
        Assert.Contains("Chưa có biên bản kiểm kê.", stocktakes, StringComparison.Ordinal);
        Assert.Contains("Lập Biên Bản Kiểm Kê", stocktakes, StringComparison.Ordinal);
    }

    private static StocktakeSheet Sheet(Guid location, Guid item) =>
        StocktakeSheet.TryCreate(EmployeeRole.WarehouseKeeper, location, item, 5, 4, "đếm lại", "BB-01", null, out var sheet) == StockDecision.Accepted ? sheet! : throw new InvalidOperationException();

    [Fact]
    public async Task StocktakeTab_FiltersByDocumentType_AndByCode_KeepingTheFilterInLinksAndForms()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var location = Guid.CreateVersion7();
        var sheet = Sheet(location, Guid.CreateVersion7());
        inventory.Stocktakes.Add(sheet);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var all = Decode(await (await client.GetAsync("/inventory?view=stocktakes")).Content.ReadAsStringAsync());
        Assert.Contains("id=\"stocktake-filter\"", all, StringComparison.Ordinal);
        Assert.Contains("name=\"type\"", all, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", all, StringComparison.Ordinal);
        Assert.Contains(Pancras.Domain.Inventory.StockDocumentCode.Of("KK", sheet.Id), all, StringComparison.Ordinal);

        var kindOnly = Decode(await (await client.GetAsync("/inventory?view=stocktakes&type=dc")).Content.ReadAsStringAsync());
        Assert.DoesNotContain(Pancras.Domain.Inventory.StockDocumentCode.Of("KK", sheet.Id), kindOnly, StringComparison.Ordinal);
        Assert.Contains("Không có phiếu khớp bộ lọc.", kindOnly, StringComparison.Ordinal);
        Assert.Contains("Bỏ lọc", kindOnly, StringComparison.Ordinal);

        var byCode = Decode(await (await client.GetAsync("/inventory?view=stocktakes&type=kk&code=" + Pancras.Domain.Inventory.StockDocumentCode.Of("kk", sheet.Id).ToLowerInvariant())).Content.ReadAsStringAsync());
        Assert.Contains(Pancras.Domain.Inventory.StockDocumentCode.Of("KK", sheet.Id), byCode, StringComparison.Ordinal);

        // Form duyệt phiếu gửi kèm bộ lọc để sau khi duyệt quay lại đúng danh sách.
        Assert.Matches(@"action=""/inventory\?handler=ApproveStocktake&view=stocktakes&type=kk&code=[^""]*""", byCode);

        var unknownType = Decode(await (await client.GetAsync("/inventory?view=stocktakes&type=abc")).Content.ReadAsStringAsync());
        Assert.Contains(Pancras.Domain.Inventory.StockDocumentCode.Of("KK", sheet.Id), unknownType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InventoryActionForms_CarryTheTabFilters()
    {
        // Lỗi UAT: thao tác chuyển kho và lập phiếu tải lại trang mà mất bộ lọc.
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var transfers = Decode(await (await client.GetAsync("/inventory?view=transfers&ts=Pending&tq=abc&size=50")).Content.ReadAsStringAsync());
        var request = Regex.Match(transfers, @"<form[^>]*action=""(/inventory\?handler=RequestTransfer[^""]*)""").Groups[1].Value;
        Assert.Contains("view=transfers", request, StringComparison.Ordinal);
        Assert.Contains("ts=Pending", request, StringComparison.Ordinal);
        Assert.Contains("tq=abc", request, StringComparison.Ordinal);
        Assert.Contains("size=50", request, StringComparison.Ordinal);

        var stocktakes = Decode(await (await client.GetAsync("/inventory?view=stocktakes&type=kk&code=1A&size=50")).Content.ReadAsStringAsync());
        var create = Regex.Match(stocktakes, @"<form[^>]*action=""(/inventory\?handler=CreateStocktake[^""]*)""").Groups[1].Value;
        Assert.Contains("view=stocktakes", create, StringComparison.Ordinal);
        Assert.Contains("type=kk", create, StringComparison.Ordinal);
        Assert.Contains("code=1A", create, StringComparison.Ordinal);
        Assert.Contains("size=50", create, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATransferAction_ReturnsToTheFilteredTransferList_WithTheSlipOpen()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var me = _factory.Services.GetRequiredService<FakeEmployeeRepository>().Employees.Single(employee => employee.Code == "admin");
        var slip = TransferSlip.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "chuyển thử", [(Guid.CreateVersion7(), 1)], me.Id)!;
        inventory.Transfers.Add(slip);
        var token = await TokenAsync(client, "/inventory?view=transfers");

        var response = await client.PostAsync(
            "/inventory?handler=CancelTransfer&view=transfers&ts=Pending&tq=abc&tp=2&size=50",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["DocumentId"] = slip.Id.ToString(),
                ["reason"] = "không cần nữa"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains("view=transfers", location, StringComparison.Ordinal);
        Assert.Contains("ts=Pending", location, StringComparison.Ordinal);
        Assert.Contains("tq=abc", location, StringComparison.Ordinal);
        Assert.Contains("tp=2", location, StringComparison.Ordinal);
        Assert.Contains("size=50", location, StringComparison.Ordinal);
        Assert.Contains("open=" + slip.Id, location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StocktakeTab_ListsAdjustmentsAndDisposals_AndFiltersThemByType()
    {
        // Kho giả trước đây luôn trả rỗng cho phiếu điều chỉnh và phiếu hủy, nên trang chưa từng được test với hai loại này.
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var location = Guid.CreateVersion7();
        var item = Guid.CreateVersion7();
        var adjustment = QuantityAdjustment.TryRecordTransferVariance(Guid.CreateVersion7(), location, item, -1, "thiếu khi nhận", "CK-000001")!;
        Assert.Equal(StockDecision.Accepted, DisposalSlip.TryCreate(location, item, 1, "vỡ", "BB-02", null, EmployeeRole.WarehouseKeeper, out var disposal));
        inventory.Adjustments.Add(adjustment);
        inventory.Disposals.Add(disposal!);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var all = Decode(await (await client.GetAsync("/inventory?view=stocktakes")).Content.ReadAsStringAsync());
        var adjustmentsOnly = Decode(await (await client.GetAsync("/inventory?view=stocktakes&type=dc")).Content.ReadAsStringAsync());

        Assert.Contains("id=\"document-" + adjustment.Id + "\"", all, StringComparison.Ordinal);
        Assert.Contains("id=\"document-" + disposal!.Id + "\"", all, StringComparison.Ordinal);
        Assert.Contains("id=\"document-" + adjustment.Id + "\"", adjustmentsOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"document-" + disposal.Id + "\"", adjustmentsOnly, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_WritesTheThreeFilteredRows()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var location = Guid.CreateVersion7();
        inventory.Rows.Add(Row("SP01", "Vòi", ProductCategory.SanitaryEquipment, location));
        inventory.Rows.Add(Row("SP02", "Lavabo", ProductCategory.SanitaryEquipment, location));
        inventory.Rows.Add(Row("SP03", "Sen", ProductCategory.SanitaryEquipment, location));
        inventory.Rows.Add(Row("NT01", "Tủ", ProductCategory.BathroomFurniture, location));
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var page = await client.GetAsync("/inventory?category=" + ProductCategoryToken.Sanitary);
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync(
            "/inventory?handler=Export&category=" + ProductCategoryToken.Sanitary,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["category"] = ProductCategoryToken.Sanitary,
                ["__RequestVerificationToken"] = token
            }));
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('\uFEFF');
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, lines.Length);
        Assert.Contains("SP01", text, StringComparison.Ordinal);
        Assert.Contains("SP02", text, StringComparison.Ordinal);
        Assert.Contains("SP03", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NT01", text, StringComparison.Ordinal);
        Assert.DoesNotContain("VNĐ", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestTransfer_CreatesAPendingSlip_WithoutMovingStock()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var sourceLocation = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var destinationLocation = StockLocation.Create("Kho B", StockLocationKind.Warehouse);
        inventory.Locations.Add(sourceLocation);
        inventory.Locations.Add(destinationLocation);
        var source = sourceLocation.Id;
        var destination = destinationLocation.Id;
        var item = Guid.CreateVersion7();
        var here = StockBalance.Create(item, source, 10, 0, 0);
        inventory.Balances.Add(here);
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var page = await client.GetAsync("/inventory?view=transfers");
        var token = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync(
            "/inventory?handler=RequestTransfer",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Transfer.SourceLocationId"] = source.ToString(),
                ["Transfer.DestinationLocationId"] = destination.ToString(),
                ["Transfer.Lines[0].CatalogItemId"] = item.ToString(),
                ["Transfer.Lines[0].Quantity"] = "3"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var slip = Assert.Single(inventory.Transfers);
        Assert.Equal(TransferStatus.Requested, slip.Status);
        Assert.Equal(10, here.OnHand);
        Assert.DoesNotContain(inventory.Balances, balance => balance.StockLocationId == destination);
        Assert.Empty(inventory.Movements);
    }

    [Theory]
    [InlineData("ApproveStocktake")]
    [InlineData("ApproveAdjustment")]
    [InlineData("ApproveDisposal")]
    [InlineData("DecideTransfer")]
    public async Task ApprovalPost_ByAWarehouseKeeper_GoesToAccessDenied(string handler)
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, EmployeeRole.WarehouseKeeper, "kho");
        var token = await TokenAsync(client, "/inventory?view=stocktakes");

        var response = await client.PostAsync(
            "/inventory?handler=" + handler,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["DocumentId"] = Guid.NewGuid().ToString(),
                ["approve"] = "true"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/access-denied", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "Số đếm chưa được nhập.")]
    [InlineData("abc", "Số đếm phải là số nguyên không âm.")]
    [InlineData("-1", "Số đếm phải là số nguyên không âm.")]
    public async Task CreateStocktake_WithABadCount_ShowsAMessage_AndSavesNothing(string count, string message)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var token = await TokenAsync(client, "/inventory?view=stocktakes");

        var response = await client.PostAsync(
            "/inventory?handler=CreateStocktake",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Stocktake.StockLocationId"] = Guid.NewGuid().ToString(),
                ["Stocktake.CatalogItemId"] = Guid.NewGuid().ToString(),
                ["Stocktake.CountedQuantity"] = count
            }));
        var text = Decode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, text, StringComparison.Ordinal);
        Assert.Empty(inventory.Stocktakes);
    }

    [Theory]
    [InlineData("stocktake", "CreateStocktake", "Stocktake")]
    [InlineData("adjustment", "CreateAdjustment", "Adjustment")]
    [InlineData("disposal", "CreateDisposal", "Disposal")]
    public async Task ARejectedStockDocument_ReopensItsOwnDialog_WithTheErrorInsideAndTheTypedValuesKept(string kind, string handler, string prefix)
    {
        // Lỗi UAT: lưu biên bản/phiếu lỗi thì modal đóng, lỗi hiện ngoài trang và các ô đã nhập bị mất.
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var token = await TokenAsync(client, "/inventory?view=stocktakes");
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            [prefix + ".StockLocationId"] = string.Empty,
            [prefix + ".CatalogItemId"] = string.Empty,
            [prefix + ".Reason"] = "lý do thử",
            [prefix + ".DocumentRef"] = "CT-123"
        };
        if (kind == "stocktake")
        {
            fields["Stocktake.CountedQuantity"] = "abc";
        }

        var response = await client.PostAsync("/inventory?handler=" + handler, new FormUrlEncodedContent(fields));
        var html = Decode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"data-show-{kind}-dialog=\"true\"", html, StringComparison.Ordinal);
        var dialog = Regex.Match(html, $"<dialog id=\"{kind}-dialog\".*?</dialog>", RegexOptions.Singleline).Value;
        var alert = Regex.Match(dialog, "role=\"alert\"[^>]*>\\s*<p[^>]*>(.*?)</p>", RegexOptions.Singleline).Groups[1].Value.Trim();
        Assert.False(string.IsNullOrEmpty(alert), "Lỗi phải hiện bên trong modal.");
        Assert.Single(Regex.Matches(html, Regex.Escape(alert)));
        Assert.Contains("value=\"lý do thử\"", dialog, StringComparison.Ordinal);
        Assert.Contains("value=\"CT-123\"", dialog, StringComparison.Ordinal);
        Assert.Empty(inventory.Stocktakes);
    }

    private static async Task<string> TokenAsync(HttpClient client, string path) =>
        Regex.Match(await (await client.GetAsync(path)).Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    [Theory]
    [InlineData(EmployeeRole.WarehouseKeeper, true)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead, true)]
    [InlineData(EmployeeRole.Accountant, true)]
    [InlineData(EmployeeRole.Sales, false)]
    public async Task Sidebar_ShowsPurchasing_ForTheRolesThatCanOpenThePage(string role, bool shown)
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, role, "nv");

        var html = await (await client.GetAsync("/inventory")).Content.ReadAsStringAsync();

        Assert.Equal(shown, html.Contains("href=\"/purchasing\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AccessDenied_IsWrittenToTheAuditLog_ForADeniedApprovalPost()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        var audit = (NoAuditLog)_factory.Services.GetRequiredService<Pancras.Application.Auditing.IAuditLog>();
        audit.Entries.Clear();
        await SignInAsync(client, EmployeeRole.WarehouseKeeper, "kho");
        var token = await TokenAsync(client, "/inventory?view=stocktakes");

        var response = await client.PostAsync(
            "/inventory?handler=ApproveStocktake",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["DocumentId"] = Guid.NewGuid().ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var denied = Assert.Single(audit.Entries, entry => entry.Action == Pancras.Domain.Auditing.AuditActions.AccessDenied);
        Assert.Contains("ApproveStocktake", denied.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(audit.Entries, entry => entry.Action == Pancras.Domain.Auditing.AuditActions.AccessDenied && entry.Detail!.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EveryPostFormOnTheInventoryPage_CarriesAnAntiforgeryToken()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var html = await (await client.GetAsync("/inventory?view=transfers")).Content.ReadAsStringAsync();
        var forms = Regex.Matches(html, "<form[^>]*method=\"post\"[^>]*>(.*?)</form>", RegexOptions.Singleline);

        Assert.NotEmpty(forms);
        Assert.All(forms, form => Assert.Contains("__RequestVerificationToken", form.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EveryPostFormInsideATransferDetail_CarriesAnAntiforgeryToken()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var slip = SeedRequestedSlip(inventory);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var html = await (await client.GetAsync("/inventory?view=transfers&open=" + slip.Id)).Content.ReadAsStringAsync();
        var dialog = Regex.Match(html, "<dialog id=\"transfer-" + slip.Id + "\".*?</dialog>", RegexOptions.Singleline).Value;
        var forms = Regex.Matches(dialog, "<form[^>]*method=\"post\"[^>]*>(.*?)</form>", RegexOptions.Singleline);

        Assert.NotEmpty(dialog);
        Assert.True(forms.Count >= 1, "Phiếu chờ duyệt phải có form duyệt/từ chối.");
        Assert.All(forms, form => Assert.Contains("__RequestVerificationToken", form.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransferList_IsATable_WithStatusColour_History_AndConfirmedActions()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var slip = SeedRequestedSlip(inventory);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var html = await (await client.GetAsync("/inventory?view=transfers")).Content.ReadAsStringAsync();
        var text = Decode(html);
        var dialog = Decode(Regex.Match(html, "<dialog id=\"transfer-" + slip.Id + "\".*?</dialog>", RegexOptions.Singleline).Value);

        Assert.Contains("id=\"transfer-table\"", html, StringComparison.Ordinal);
        Assert.Contains("bg-amber-100 text-amber-800", html, StringComparison.Ordinal);
        Assert.Contains("Chờ bạn duyệt", text, StringComparison.Ordinal);
        Assert.Contains("Chờ tôi duyệt", text, StringComparison.Ordinal);
        Assert.Contains("Lịch sử phiếu", dialog, StringComparison.Ordinal);
        Assert.Contains("Lập phiếu đề nghị", dialog, StringComparison.Ordinal);
        Assert.Contains("AnNK", dialog, StringComparison.Ordinal);
        Assert.Contains("data-confirm", dialog, StringComparison.Ordinal);
        Assert.Contains("data-reason=\"required\"", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Hủy phiếu", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Trả về kho xuất", dialog, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "id=\"open-transfer\""));
    }

    [Fact]
    public async Task TransferFilter_IsLive_AndTheResultsIncludeTheRowDialogs()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var slip = SeedRequestedSlip(inventory);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var html = await (await client.GetAsync("/inventory?view=transfers&ts=Requested")).Content.ReadAsStringAsync();

        var form = Regex.Match(html, "<form[^>]*id=\"transfer-filter\"[^>]*>").Value;
        Assert.Contains("data-live-filter", form, StringComparison.Ordinal);
        var results = html.IndexOf("id=\"live-results\"", StringComparison.Ordinal);
        Assert.True(results > 0);
        Assert.True(html.IndexOf("id=\"transfer-table\"", StringComparison.Ordinal) > results);
        // Hộp thoại chi tiết của từng dòng nằm trong vùng kết quả, để dòng mới hiện ra sau khi lọc vẫn bấm mở được.
        Assert.True(html.IndexOf("<dialog id=\"transfer-" + slip.Id + "\"", StringComparison.Ordinal) > results);
        Assert.Single(Regex.Matches(html, "id=\"live-results\""));
        Assert.DoesNotContain(">Lọc</button>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransferFilter_OffersAClearLink_OnlyWhileFiltering_InsideTheResults()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        SeedRequestedSlip(inventory);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var plain = await (await client.GetAsync("/inventory?view=transfers")).Content.ReadAsStringAsync();
        var filtered = await (await client.GetAsync("/inventory?view=transfers&ts=Approved")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Xóa lọc", plain, StringComparison.Ordinal);
        Assert.Contains("Xóa lọc", filtered, StringComparison.Ordinal);
        Assert.True(filtered.IndexOf("Xóa lọc", StringComparison.Ordinal) > filtered.IndexOf("id=\"live-results\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransferList_FiltersByStatus()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        SeedRequestedSlip(inventory);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var none = Decode(await (await client.GetAsync("/inventory?view=transfers&ts=Approved")).Content.ReadAsStringAsync());
        var some = Decode(await (await client.GetAsync("/inventory?view=transfers&ts=Requested")).Content.ReadAsStringAsync());

        Assert.Contains("Không có phiếu phù hợp.", none, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"transfer-table\"", none, StringComparison.Ordinal);
        Assert.Contains("id=\"transfer-table\"", some, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SalesSeeStockOfEveryLocation_ButNeverTheCost()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        inventory.Rows.Add(Row("SP01", "Vòi", ProductCategory.SanitaryEquipment, Guid.CreateVersion7()));
        await SignInAsync(client, EmployeeRole.Sales, "sales");

        var html = Decode(await (await client.GetAsync("/inventory")).Content.ReadAsStringAsync());

        Assert.Contains("SP01", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Giá vốn", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EmployeeRole.Sales, false)]
    [InlineData(EmployeeRole.WarehouseKeeper, false)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead, false)]
    [InlineData(EmployeeRole.CustomerCare, false)]
    [InlineData(EmployeeRole.Accountant, true)]
    [InlineData(EmployeeRole.Director, true)]
    [InlineData(EmployeeRole.Admin, true)]
    public async Task CostValue_AppearsInPageHtml_OnlyForFinanceRoles(string role, bool seesCost)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        inventory.Rows.Add(Row("SP01", "Vòi", ProductCategory.SanitaryEquipment, Guid.CreateVersion7()) with { CostPrice = 7654321m });
        await SignInAsync(client, role, "roletest");

        var response = await client.GetAsync("/inventory");
        var html = Decode(await response.Content.ReadAsStringAsync());

        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(seesCost, html.Contains("7.654.321", StringComparison.Ordinal));
            if (!seesCost)
            {
                Assert.DoesNotContain("7654321", html, StringComparison.Ordinal);
            }
        }
        else
        {
            // Vai không được mở trang thì không có dữ liệu nào để lộ.
            Assert.False(seesCost);
            Assert.DoesNotContain("7.654.321", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(EmployeeRole.Sales)]
    [InlineData(EmployeeRole.WarehouseKeeper)]
    [InlineData(EmployeeRole.Accountant)]
    [InlineData(EmployeeRole.Admin)]
    public async Task CsvExport_NeverCarriesTheCostValue_ForAnyRole(string role)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        inventory.Rows.Add(Row("SP01", "Vòi", ProductCategory.SanitaryEquipment, Guid.CreateVersion7()) with { CostPrice = 7654321m });
        await SignInAsync(client, role, "roletest");
        var page = await client.GetAsync("/inventory");
        var token = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync(
            "/inventory?handler=Export",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());

        Assert.DoesNotContain("7654321", text, StringComparison.Ordinal);
        Assert.DoesNotContain("7.654.321", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Giá vốn", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Balances_ArePaged_TwentyAtATime_WithAPagerThatKeepsTheFilter()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var location = Guid.CreateVersion7();
        for (var i = 1; i <= 25; i++)
        {
            inventory.Rows.Add(Row("SP" + i.ToString("00"), "Vòi " + i, ProductCategory.SanitaryEquipment, location));
        }

        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var first = Decode(await (await client.GetAsync("/inventory?category=" + ProductCategoryToken.Sanitary)).Content.ReadAsStringAsync());
        var second = Decode(await (await client.GetAsync("/inventory?category=" + ProductCategoryToken.Sanitary + "&bp=2")).Content.ReadAsStringAsync());
        var wide = Decode(await (await client.GetAsync("/inventory?size=50")).Content.ReadAsStringAsync());

        Assert.Contains("SP01", first, StringComparison.Ordinal);
        Assert.Contains("SP20", first, StringComparison.Ordinal);
        Assert.DoesNotContain("SP21", first, StringComparison.Ordinal);
        Assert.Contains("Hiển thị 1–20 / 25", first, StringComparison.Ordinal);
        Assert.Contains("category=" + ProductCategoryToken.Sanitary + "&bp=2", first, StringComparison.Ordinal);
        Assert.DoesNotContain("SP20", second, StringComparison.Ordinal);
        Assert.Contains("SP21", second, StringComparison.Ordinal);
        Assert.Contains("SP25", second, StringComparison.Ordinal);
        Assert.Contains("Hiển thị 21–25 / 25", second, StringComparison.Ordinal);
        Assert.Contains("SP25", wide, StringComparison.Ordinal);
        Assert.Contains("25 mặt hàng", first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StocktakeForms_TakeRealFiles_InsteadOfAFreeTextImageReference()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var html = await (await client.GetAsync("/inventory?view=stocktakes")).Content.ReadAsStringAsync();
        var forms = Regex.Matches(html, "<form[^>]*handler=Create(?:Stocktake|Adjustment|Disposal)[^>]*>(.*?)</form>", RegexOptions.Singleline);

        Assert.Equal(3, forms.Count);
        Assert.All(forms, form =>
        {
            Assert.Contains("multipart/form-data", form.Value, StringComparison.Ordinal);
            Assert.Contains("type=\"file\" name=\"files\"", form.Value, StringComparison.Ordinal);
        });
        Assert.DoesNotContain("ImageRef", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReceiveForm_OffersAFillAllButton_ThatStillLeavesEveryQuantityRequired()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var slip = SeedRequestedSlip(inventory);
        var item = slip.Lines[0].CatalogItemId;
        var source = StockBalance.Create(item, slip.SourceLocationId, 10, 0, 0);
        var destination = StockBalance.Create(item, slip.DestinationLocationId, 0, 0, 0);
        var sources = new Dictionary<Guid, StockBalance> { [item] = source };
        Assert.True(slip.TryApprove(sources));
        Assert.True(slip.TryConfirmShipment(sources, new Dictionary<Guid, StockBalance> { [item] = destination }));
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var html = await (await client.GetAsync("/inventory?view=transfers&open=" + slip.Id)).Content.ReadAsStringAsync();

        Assert.Contains("data-fill-received", html, StringComparison.Ordinal);
        Assert.Contains("data-shipped=\"2\"", html, StringComparison.Ordinal);
        Assert.Matches("<input type=\"number\" min=\"0\" required name=\"receiveLines\\[0\\]\\.Quantity\"", html);
    }

    private static TransferSlip SeedRequestedSlip(InventoryDirectoryDouble inventory)
    {
        var from = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var to = StockLocation.Create("Showroom B", StockLocationKind.Showroom);
        inventory.Locations.Add(from);
        inventory.Locations.Add(to);
        var slip = TransferSlip.Create(from.Id, to.Id, "Bù tồn", [(Guid.CreateVersion7(), 2)], Guid.CreateVersion7())!;
        inventory.Transfers.Add(slip);
        inventory.TransferEvents.Add(TransferEvent.Create(slip.Id, TransferEventKind.Requested, "AnNK", DateTimeOffset.UtcNow, "Bù tồn"));
        return slip;
    }

    private static StockBalanceRow Row(string code, string name, ProductCategory category, Guid locationId) =>
        new(Guid.CreateVersion7(), code, name, "Toto", category, locationId, "Kho A", 3, 1, 2, 4);

    [Theory]
    [InlineData("balances", "help-inventory-balances", "Tra cứu tồn kho")]
    [InlineData("transfers", "help-inventory-transfers", "In phiếu xuất kho")]
    [InlineData("stocktakes", "help-inventory-stocktakes", "Kiểm kê, điều chỉnh và hủy hàng")]
    public async Task EachInventoryTab_HasItsOwnHelpGuide(string view, string dialogId, string expectedText)
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var text = Decode(await (await client.GetAsync("/inventory?view=" + view)).Content.ReadAsStringAsync());

        Assert.Contains("data-open-dialog=\"" + dialogId + "\"", text, StringComparison.Ordinal);
        Assert.Contains("id=\"" + dialogId + "\"", text, StringComparison.Ordinal);
        Assert.Contains(expectedText, text, StringComparison.Ordinal);
        Assert.Contains("4. Phân quyền", text, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(text, "data-open-dialog=\"help-"));
    }

    [Fact]
    public async Task TransferPrint_AnonymousIsRedirectedToLogin()
    {
        using var client = _factory.CreateClient(NoRedirect);

        var response = await client.GetAsync("/inventory/transfers/" + Guid.NewGuid() + "/print");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransferPrint_ShowsAnApprovedSlip_AndHidesRequestedOnes()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var source = StockLocation.Create("Kho tổng", StockLocationKind.Warehouse);
        var destination = StockLocation.Create("Showroom Q1", StockLocationKind.Showroom);
        inventory.Locations.Add(source);
        inventory.Locations.Add(destination);
        var item = Guid.CreateVersion7();
        var approved = TransferSlip.Create(source.Id, destination.Id, "Xe sáng", [(item, 3)])!;
        var balance = StockBalance.Create(item, source.Id, 10, 0, 0);
        Assert.True(approved.TryApprove(new Dictionary<Guid, StockBalance> { [item] = balance }));
        var requested = TransferSlip.Create(source.Id, destination.Id, null, [(item, 1)])!;
        inventory.Transfers.Add(approved);
        inventory.Transfers.Add(requested);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var printed = await client.GetAsync("/inventory/transfers/" + approved.Id + "/print");
        var text = Decode(await printed.Content.ReadAsStringAsync());
        var hidden = await client.GetAsync("/inventory/transfers/" + requested.Id + "/print");

        Assert.Equal(HttpStatusCode.OK, printed.StatusCode);
        Assert.Contains("Phiếu xuất kho", text, StringComparison.Ordinal);
        Assert.Contains("Kho tổng", text, StringComparison.Ordinal);
        Assert.Contains("Showroom Q1", text, StringComparison.Ordinal);
        Assert.Contains("Thực nhận", text, StringComparison.Ordinal);
        Assert.Contains("Thủ kho xuất", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Giá vốn", text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task TransferPrint_AKeeperWithoutAnAssignedLocation_SeesNothing()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var source = StockLocation.Create("Kho tổng", StockLocationKind.Warehouse);
        var destination = StockLocation.Create("Showroom Q1", StockLocationKind.Showroom);
        inventory.Locations.Add(source);
        inventory.Locations.Add(destination);
        var item = Guid.CreateVersion7();
        var slip = TransferSlip.Create(source.Id, destination.Id, null, [(item, 3)])!;
        Assert.True(slip.TryApprove(new Dictionary<Guid, StockBalance> { [item] = StockBalance.Create(item, source.Id, 10, 0, 0) }));
        inventory.Transfers.Add(slip);
        await SignInAsync(client, EmployeeRole.WarehouseKeeper, "kho");

        var response = await client.GetAsync("/inventory/transfers/" + slip.Id + "/print");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private InventoryDirectoryDouble Reset()
    {
        var inventory = _factory.Services.GetRequiredService<InventoryDirectoryDouble>();
        inventory.Rows.Clear();
        inventory.Locations.Clear();
        inventory.Transfers.Clear();
        inventory.TransferEvents.Clear();
        inventory.Stocktakes.Clear();
        inventory.Adjustments.Clear();
        inventory.Disposals.Clear();
        inventory.Balances.Clear();
        inventory.Movements.Clear();
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        employees.Employees.Clear();
        return inventory;
    }

    private async Task SignInAsync(HttpClient client, string role, string code)
    {
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.RemoveAll(employee => employee.NormalizedCode == code.ToUpperInvariant());
        employees.Employees.Add(Employee.Create(code, code, code.ToUpperInvariant(), hasher.Hash("Admin@12345"), role));
        var login = await client.GetAsync("/login");
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = code,
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        });
        await client.PostAsync("/login", form);
    }

    private static string Decode(string html) => System.Net.WebUtility.HtmlDecode(html);

    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };
}
