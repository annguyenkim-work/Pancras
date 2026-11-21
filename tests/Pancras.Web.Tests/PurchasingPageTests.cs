using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Purchasing;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;
using Pancras.Web.Pages.Purchasing;

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
    public async Task Purchasing_HasAHelpButton_OpeningTheFourSectionGuide()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client);

        var text = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing")).Content.ReadAsStringAsync());

        Assert.Contains("data-open-dialog=\"help-purchasing\"", text, StringComparison.Ordinal);
        Assert.Contains("id=\"help-purchasing\"", text, StringComparison.Ordinal);
        Assert.Contains("1. Mục tiêu", text, StringComparison.Ordinal);
        Assert.Contains("2. Các chức năng", text, StringComparison.Ordinal);
        Assert.Contains("3. Quy trình thực hiện", text, StringComparison.Ordinal);
        Assert.Contains("4. Phân quyền", text, StringComparison.Ordinal);
        Assert.Contains("Nhập Kho & Cập Nhật Giá Vốn", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderList_IsPaged_With20Or50PerPage()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var location = Guid.CreateVersion7();
        for (var index = 1; index <= 25; index++)
        {
            orders.Orders.Add(NewOrder("PO-" + index.ToString("00"), location));
        }

        await SignInAsync(client);

        var first = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing")).Content.ReadAsStringAsync());
        var second = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?pageNumber=2")).Content.ReadAsStringAsync());
        var wide = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?size=50")).Content.ReadAsStringAsync());

        Assert.Contains("Hiển thị 1–20 / 25", first, StringComparison.Ordinal);
        Assert.Contains("Hiển thị 21–25 / 25", second, StringComparison.Ordinal);
        Assert.Contains("Hiển thị 1–25 / 25", wide, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", first, StringComparison.Ordinal);
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
        Assert.Equal(ReceiptDecision.Ok, order.TryRecordReceipt(order.Lines.ToDictionary(line => line.Id, line => line.Quantity), true, null, DateTimeOffset.UtcNow, "mua", out _));
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
        Assert.Contains("id=\"purchase-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("/js/purchasing-form.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dialog open", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedAction_ShowsItsReasonAsAnErrorToast_NotAsABannerAtTheTopOfThePage()
    {
        // Lỗi UAT: nhận hàng bị từ chối, trang tải lại mà người dùng (đang ở giữa trang) không thấy lỗi vì banner nằm ở đầu trang.
        // Mọi lỗi sau POST đi một đường: toast ghi ngay khi xử lý, địa chỉ không mang tham số error=.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-DRAFT", Guid.CreateVersion7());
        orders.Orders.Add(order);
        await SignInAsync(client);

        var response = await client.PostAsync("/purchasing?handler=Receive", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TokenAsync(client, order.Id),
            ["orderId"] = order.Id.ToString()
        }));
        var location = response.Headers.Location!.OriginalString;
        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync(location)).Content.ReadAsStringAsync());

        Assert.DoesNotContain("error=", location, StringComparison.Ordinal);
        var toast = Regex.Match(html, "<div[^>]*data-tone=\"rose\"[^>]*>.*?</div>", RegexOptions.Singleline);
        Assert.True(toast.Success, "Lỗi phải hiện bằng toast đỏ (góc dưới phải).");
        Assert.Contains("chưa nhận hàng được", toast.Value, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, Regex.Escape("chưa nhận hàng được")));
    }

    [Fact]
    public async Task ReceivingMoreThanStillToReceive_IsRejected_WithAToastNamingTheItem_AndTheInputCapsTheQuantity()
    {
        // Chủ dự án chốt 2026-11-21: không bao giờ nhận quá số đặt, kể cả khi đóng đơn.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var catalog = _factory.Services.GetRequiredService<CatalogDirectoryDouble>();
        var item = CatalogItem.Create("SPVUOT", "Bồn", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", null, null, null, null, 9m, 1m, 1m, 1m, 1m);
        catalog.Items.Add(item);
        var order = PurchaseOrder.TryCreate("PO-VUOT", Guid.CreateVersion7(), Guid.CreateVersion7(), "CONT", null, null, null, 1m, 0m, [(item.Id, 4, 10m, 0m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        orders.Orders.Add(order);
        await SignInAsync(client);

        var page = await (await client.GetAsync("/purchasing?order=" + order.Id)).Content.ReadAsStringAsync();
        Assert.Matches(@"name=""actuals\[" + order.Lines[0].Id + @"\]""[^>]*max=""4""", page);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/purchasing?handler=Receive", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["orderId"] = order.Id.ToString(),
            ["closeOrder"] = "true",
            ["note"] = "thừa hàng",
            ["actuals[" + order.Lines[0].Id + "]"] = "6"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(PurchaseStatus.Approved, order.Status);

        var after = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync(response.Headers.Location!.OriginalString)).Content.ReadAsStringAsync());
        var toast = Regex.Match(after, "<div[^>]*data-tone=\"rose\"[^>]*>.*?</div>", RegexOptions.Singleline).Value;
        Assert.Contains("SPVUOT", toast, StringComparison.Ordinal);
        Assert.Contains("6", toast, StringComparison.Ordinal);
        Assert.Contains("còn phải nhận 4", toast, StringComparison.Ordinal);
    }

    [Fact]
    public void ExceedsOrderedMessage_NamesItemAndLimit_FromTheTypedDetail()
    {
        Assert.Equal(
            "Mặt hàng SP01: nhập 12 nhưng chỉ còn phải nhận 10. Không được nhận quá số đặt; sửa số thực nhận rồi thử lại.",
            PurchaseErrorMessages.Describe(PurchaseError.ReceiptExceedsOrdered, [new ReceiptExcess("SP01", 12, 10)]));
        Assert.Equal(
            "Có mặt hàng nhận nhiều hơn số còn phải nhận. Không được nhận quá số đặt; sửa số thực nhận rồi thử lại.",
            PurchaseErrorMessages.Describe(PurchaseError.ReceiptExceedsOrdered));
    }

    [Fact]
    public async Task ActionsOnAnOrder_ReturnToTheSameFilteredList()
    {
        // Lỗi UAT: duyệt, nhận hàng (một phần, đủ) tải lại trang mà mất bộ lọc.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-KEEP", Guid.CreateVersion7());
        orders.Orders.Add(order);
        await SignInAsync(client);
        const string list = "q=PO-KEEP&status=Draft&size=50";

        var page = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + order.Id + "&" + list)).Content.ReadAsStringAsync());
        var action = Regex.Match(page, @"<form[^>]*action=""(/purchasing\?handler=ChangeStatus[^""]*)""").Groups[1].Value;
        Assert.Contains("q=PO-KEEP", action, StringComparison.Ordinal);
        Assert.Contains("status=Draft", action, StringComparison.Ordinal);
        Assert.Contains("size=50", action, StringComparison.Ordinal);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync(action, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["orderId"] = order.Id.ToString(),
            ["action"] = ((int)PurchaseStatusAction.Submit).ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains("order=" + order.Id, location, StringComparison.Ordinal);
        Assert.Contains("notice=submitted", location, StringComparison.Ordinal);
        Assert.Contains("q=PO-KEEP", location, StringComparison.Ordinal);
        Assert.Contains("status=Draft", location, StringComparison.Ordinal);
        Assert.Contains("size=50", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedReceipt_AlsoReturnsToTheSameFilteredList()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-KEEP2", Guid.CreateVersion7());
        orders.Orders.Add(order);
        await SignInAsync(client);

        var response = await client.PostAsync("/purchasing?handler=Receive&q=PO-KEEP2&status=Approved&pageNumber=2", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TokenAsync(client, order.Id),
            ["orderId"] = order.Id.ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.DoesNotContain("error=", location, StringComparison.Ordinal);
        Assert.Contains("q=PO-KEEP2", location, StringComparison.Ordinal);
        Assert.Contains("status=Approved", location, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewOrderAndFormCloseLinks_KeepTheFilteredList()
    {
        // Rà toàn bộ (2026-11-21): nút "Tạo đơn" và nút đóng form (×, Quay lại) đưa về /purchasing trần, mất bộ lọc.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-LINK", Guid.CreateVersion7());
        orders.Orders.Add(order);
        await SignInAsync(client);

        var list = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?q=PO&status=Draft&size=50")).Content.ReadAsStringAsync());
        Assert.Contains("href=\"/purchasing?form=1&q=PO&status=Draft&size=50\"", list, StringComparison.Ordinal);

        var create = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?form=1&q=PO&status=Draft&size=50")).Content.ReadAsStringAsync());
        var closeLinks = Regex.Matches(create, @"<a href=""([^""]*)"" data-leave").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(2, closeLinks.Length);
        Assert.All(closeLinks, href => Assert.EndsWith("q=PO&status=Draft&size=50", href, StringComparison.Ordinal));

        var edit = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?edit=" + order.Id + "&q=PO&status=Draft")).Content.ReadAsStringAsync());
        var editClose = Regex.Matches(edit, @"<a href=""([^""]*)"" data-leave").Select(match => match.Groups[1].Value).ToArray();
        Assert.All(editClose, href => Assert.Equal("/purchasing?order=" + order.Id + "&q=PO&status=Draft", href));
    }

    [Fact]
    public async Task OpeningAnOrderFromTheFilteredList_KeepsTheFilters()
    {
        // Lỗi UAT: lọc danh sách rồi bấm một đơn để xem chi tiết thì bộ lọc bị mất.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var location = Guid.CreateVersion7();
        orders.Orders.Add(NewOrder("PO-AA", location));
        orders.Orders.Add(NewOrder("PO-AB", location));
        await SignInAsync(client);

        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?q=PO&size=50")).Content.ReadAsStringAsync());

        var links = System.Text.RegularExpressions.Regex.Matches(html, "<a href=\"(/purchasing\\?order=[^\"]*)\" class=\"block rounded-xl").Select(match => match.Groups[1].Value).Distinct().ToArray();
        Assert.Equal(2, links.Length);
        Assert.All(links, link =>
        {
            Assert.Contains("q=PO", link, StringComparison.Ordinal);
            Assert.Contains("size=50", link, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task OrderList_CanBeFilteredByStatus_AndKeepsTheStatusWhenOpeningAnOrder()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var location = Guid.CreateVersion7();
        var draft = NewOrder("PO-DRAFT", location);
        var approved = NewOrder("PO-APPROVED", location);
        Assert.True(approved.TrySubmit());
        Assert.True(approved.TryApprove());
        orders.Orders.Add(draft);
        orders.Orders.Add(approved);
        await SignInAsync(client);

        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?status=Approved")).Content.ReadAsStringAsync());

        Assert.Contains("PO-APPROVED", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PO-DRAFT", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"Approved\" selected", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"\">Mọi trạng thái</option>", html, StringComparison.Ordinal);
        var card = System.Text.RegularExpressions.Regex.Match(html, "<a href=\"(/purchasing\\?order=[^\"]*)\" class=\"block rounded-xl").Groups[1].Value;
        Assert.Contains("status=Approved", card, StringComparison.Ordinal);
        var unknown = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?status=Nonsense")).Content.ReadAsStringAsync());
        Assert.Contains("PO-DRAFT", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderDetail_PutsTheStatusInTheTitleRow_AndGivesEveryActionTheSameSize()
    {
        // UAT: trạng thái phải nằm cạnh mã đơn (sát phải) và các nút thao tác cùng chiều cao, cùng độ rộng, đủ chữ, tối đa 2 hàng.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-BTN", Guid.CreateVersion7());
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        orders.Orders.Add(order);
        await SignInAsync(client);

        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + order.Id)).Content.ReadAsStringAsync());

        var title = html.IndexOf("PO-BTN ·", StringComparison.Ordinal);
        var badge = html.IndexOf("Đã duyệt</span>", title, StringComparison.Ordinal);
        var actions = html.IndexOf("id=\"order-actions\"", StringComparison.Ordinal);
        Assert.True(title > 0 && badge > title && actions > badge, "Huy hiệu trạng thái phải đứng ngay sau mã đơn, trước khối nút thao tác.");
        var toolbar = html[actions..html.IndexOf("DANH SÁCH MẶT HÀNG", actions, StringComparison.Ordinal)];
        var controls = System.Text.RegularExpressions.Regex.Matches(toolbar, "<(?:button type=\"submit\"|a href=\"/purchasing\\?[^\"]*edit=)[^>]*class=\"([^\"]*)\"")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.True(controls.Length >= 5, "Xuất CSV, Sửa đơn, Nhập kho, Hàng đang về, Hủy đơn.");
        Assert.All(controls, classes =>
        {
            Assert.Contains("h-10", classes, StringComparison.Ordinal);
            Assert.Contains("whitespace-nowrap", classes, StringComparison.Ordinal);
            Assert.DoesNotContain("py-2", classes, StringComparison.Ordinal);
        });
        // Các nút thường cùng độ rộng; riêng nút Nhập kho có nhãn dài nên rộng theo chữ.
        Assert.Equal(1, controls.Count(classes => !classes.Contains("w-44", StringComparison.Ordinal)));
    }


    [Fact]
    public async Task ReceiveForm_KeepsTheReceiveButtonAndTheCloseCheckboxTogether_ButtonFirst()
    {
        // UAT: nút Nhập kho đứng trước, ô "Đóng đơn dù nhận thiếu" đứng sau, cùng một khối không tách dòng.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-RCV", Guid.CreateVersion7());
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        orders.Orders.Add(order);
        await SignInAsync(client);

        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + order.Id)).Content.ReadAsStringAsync());

        var form = System.Text.RegularExpressions.Regex.Match(html, "<form id=\"receive-form\"[^>]*class=\"([^\"]*)\".*?</form>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(form.Success);
        Assert.Contains("flex-nowrap", form.Groups[1].Value, StringComparison.Ordinal);
        var button = form.Value.IndexOf("Nhập Kho & Cập Nhật Giá Vốn", StringComparison.Ordinal);
        var checkbox = form.Value.IndexOf("name=\"closeOrder\"", StringComparison.Ordinal);
        Assert.True(button > 0 && checkbox > button, "Nút nhập kho phải đứng trước ô đóng đơn dù nhận thiếu.");
    }

    [Fact]
    public async Task TheOtherTaxInput_IsAMoneyInput_LikeTheChargeAmounts()
    {
        // Lỗi UAT: ô "Thuế khác (VNĐ)" hiện số thô 200000 thay vì 200.000 vì thiếu hook data-vnd (script định dạng tiền chỉ xử lý ô có thuộc tính này).
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client);

        var html = await (await client.GetAsync("/purchasing?form=1")).Content.ReadAsStringAsync();

        var input = System.Text.RegularExpressions.Regex.Match(html, "<input name=\"Form\\.Lines\\[0\\]\\.SupplementalDuty\"[^>]*>");
        Assert.True(input.Success, "Phải có ô Thuế khác của dòng đầu.");
        var classes = System.Text.RegularExpressions.Regex.Match(input.Value, "class=\"([^\"]*)\"").Groups[1].Value.Split(' ');
        Assert.Contains("line-other", classes);
        Assert.Contains("data-vnd=", input.Value, StringComparison.Ordinal);
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
        Assert.Contains("order=" + order.Id, response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Contains("File không hợp lệ", System.Net.WebUtility.HtmlDecode(await (await client.GetAsync(response.Headers.Location!.OriginalString)).Content.ReadAsStringAsync()), StringComparison.Ordinal);
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

    [Fact]
    public async Task PartiallyReceivedOrder_ShowsWhatReachedStock_AndTheWholeOrderEstimateSeparately()
    {
        // Test thủ công 2026-11-21 (B2): nhận 6/10 mà dòng "Giá vốn hàng nhập đã ghi nhận" vẫn hiện tổng cả đơn.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = TwoLineOrder("PO-MOT-PHAN");
        var first = order.Lines[0];
        var second = order.Lines[1];
        Assert.Equal(ReceiptDecision.Ok, order.TryRecordReceipt(new Dictionary<Guid, int> { [first.Id] = 2, [second.Id] = 0 }, false, null, DateTimeOffset.UtcNow, "kho", out _));
        orders.Orders.Add(order);
        await SignInAsync(client);

        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + order.Id)).Content.ReadAsStringAsync());

        var recorded = order.Lines.Sum(line => line.CostedAmount);
        var whole = order.Lines.Sum(line => PurchaseCosting.For(order, line).LandedCost);
        Assert.True(recorded > 0 && recorded < whole);
        Assert.Matches(TotalRow("Giá vốn đã ghi nhận vào kho", recorded), html);
        Assert.Matches(TotalRow("Dự toán giá vốn toàn đơn", whole), html);
        Assert.DoesNotContain("Giá vốn hàng nhập đã ghi nhận (Landed Total)", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderClosedShort_RecordsOnlyTheCostThatReachedStock()
    {
        // B2: đóng đơn dù nhận thiếu, có dòng chưa nhận cái nào: dòng đó không vào kho nên không nằm trong số đã ghi nhận.
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = TwoLineOrder("PO-DONG-THIEU");
        var first = order.Lines[0];
        var second = order.Lines[1];
        Assert.Equal(ReceiptDecision.Ok, order.TryRecordReceipt(new Dictionary<Guid, int> { [first.Id] = first.Quantity, [second.Id] = 0 }, true, "NCC giao thiếu", DateTimeOffset.UtcNow, "kho", out _));
        orders.Orders.Add(order);
        await SignInAsync(client);

        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + order.Id)).Content.ReadAsStringAsync());

        Assert.Equal(PurchaseStatus.Received, order.Status);
        Assert.Matches(TotalRow("Giá vốn hàng nhập đã ghi nhận (Landed Total)", first.CostedAmount), html);
    }

    [Fact]
    public async Task ReceiveButton_CountsOnlyTheLinesStillToReceive()
    {
        // Test thủ công 2026-11-21 (B5): còn 1 mặt hàng chưa nhận đủ mà nút vẫn ghi "(2 món)".
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = TwoLineOrder("PO-CON-MOT");
        var first = order.Lines[0];
        var second = order.Lines[1];
        Assert.Equal(ReceiptDecision.Ok, order.TryRecordReceipt(new Dictionary<Guid, int> { [first.Id] = first.Quantity, [second.Id] = 1 }, false, null, DateTimeOffset.UtcNow, "kho", out _));
        orders.Orders.Add(order);
        await SignInAsync(client);

        var html = System.Net.WebUtility.HtmlDecode(await (await client.GetAsync("/purchasing?order=" + order.Id)).Content.ReadAsStringAsync());

        Assert.Contains("Nhập Kho & Cập Nhật Giá Vốn (1 món)", html, StringComparison.Ordinal);
    }

    private static PurchaseOrder TwoLineOrder(string code)
    {
        var order = PurchaseOrder.TryCreate(
            code,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "CONT",
            "BL",
            "Hải Phòng",
            new DateOnly(2026, 11, 12),
            25000m,
            0.10m,
            [(Guid.CreateVersion7(), 10, 100m, 0.10m, 0m, 0m, 0m), (Guid.CreateVersion7(), 5, 400m, 0.20m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        return order;
    }

    private static Regex TotalRow(string label, decimal amount) =>
        new(Regex.Escape(label) + @"</span>\s*<span[^>]*>" + Regex.Escape(Pancras.Web.Formatting.VndMoney.FormatDong(amount)) + "<");

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

    [Theory]
    [InlineData(EmployeeRole.WarehouseKeeper, false)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead, false)]
    [InlineData(EmployeeRole.Accountant, true)]
    [InlineData(EmployeeRole.Director, true)]
    [InlineData(EmployeeRole.Admin, true)]
    public async Task ExchangeRateAndFobPrice_AppearInOrderHtml_OnlyForManagingRoles(string role, bool seesCost)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var location = Guid.CreateVersion7();
        var order = PurchaseOrder.TryCreate(
            "PO-GIA", Guid.CreateVersion7(), location, "CONT", "BL", "Hải Phòng", new DateOnly(2026, 11, 12),
            31337m, 0.10m, [(Guid.CreateVersion7(), 4, 987m, 0.05m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        orders.Orders.Add(order);
        await SignInAsync(client, role, location);

        var page = await client.GetAsync("/purchasing?order=" + order.Id);
        var text = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("PO-GIA", text, StringComparison.Ordinal);
        Assert.Equal(seesCost, text.Contains("31.337", StringComparison.Ordinal));
        if (!seesCost)
        {
            Assert.DoesNotContain("31337", text, StringComparison.Ordinal);
            Assert.DoesNotContain("987 USD", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(EmployeeRole.Accountant, true)]
    [InlineData(EmployeeRole.Admin, true)]
    [InlineData(EmployeeRole.WarehouseKeeper, false)]
    public async Task ReceivableOrder_ShowsTheCostPreviewPanel_OnlyToRolesThatSeeCost(string role, bool shown)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var location = Guid.CreateVersion7();
        var order = NewOrder("PO-XEM-TRUOC", location);
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        orders.Orders.Add(order);
        await SignInAsync(client, role, location);

        var page = await client.GetAsync("/purchasing?order=" + order.Id);
        var text = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(shown, text.Contains("id=\"receipt-preview\"", StringComparison.Ordinal));
        Assert.Equal(shown, text.Contains("Giá vốn dự kiến", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DraftOrder_HasNoCostPreviewPanel()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var orders = Reset();
        var order = NewOrder("PO-NHAP", Guid.CreateVersion7());
        orders.Orders.Add(order);
        await SignInAsync(client, EmployeeRole.Accountant);

        var page = await client.GetAsync("/purchasing?order=" + order.Id);
        var text = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

        Assert.DoesNotContain("id=\"receipt-preview\"", text, StringComparison.Ordinal);
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
        Assert.Contains("id=\"receive-form\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"actuals[", text, StringComparison.Ordinal);
        Assert.Contains("name=\"closeOrder\"", text, StringComparison.Ordinal);
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
