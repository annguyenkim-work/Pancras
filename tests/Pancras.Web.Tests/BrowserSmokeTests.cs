using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Web.Tests;

/// <summary>
/// Smoke test trên trình duyệt thật (docs/02-coding-rules/testing.md mục 7): những lỗi UAT chỉ thấy được khi JS chạy và trang được
/// vẽ thật. App chạy trên Kestrel với cùng các test double như test trang, nên không cần DB hay tài khoản thật. Server và trình duyệt
/// dùng chung cho cả lớp (<see cref="BrowserFixture"/>); mỗi test một browser context.
/// </summary>
[Trait("Category", "Browser")]
public sealed class BrowserSmokeTests(BrowserFixture fixture) : IClassFixture<BrowserFixture>, IAsyncLifetime
{
    private const string Password = "Admin@12345";
    private readonly List<string> _consoleErrors = [];
    private readonly List<IBrowserContext> _contexts = [];

    private PancrasWebFactory Factory => fixture.Factory;

    private string BaseUrl => fixture.BaseUrl;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var context in _contexts)
        {
            await context.DisposeAsync();
        }
    }

    private async Task<IPage> SignedInPageAsync(int width = 1440, int height = 900, string role = EmployeeRole.Admin)
    {
        var employees = Factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = Factory.Services.GetRequiredService<FakePasswordHasher>();
        var code = "smoke" + Guid.NewGuid().ToString("N")[..8];
        employees.Employees.Add(Employee.Create("Smoke", code, code.ToUpperInvariant(), hasher.Hash(Password), role));

        var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = width, Height = height } });
        _contexts.Add(context);
        var page = await context.NewPageAsync();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                _consoleErrors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => _consoleErrors.Add(error);

        await page.GotoAsync(BaseUrl + "/login");
        await page.FillAsync("input[name='Command.EmployeeCode']", code);
        await page.FillAsync("input[name='Command.Password']", Password);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.Ordinal));
        return page;
    }

    private void AssertNoScriptErrors() => Assert.True(_consoleErrors.Count == 0, "Lỗi JS trên trang:\n" + string.Join("\n", _consoleErrors));

    [Fact]
    public async Task OtherTaxInput_ShowsGroupedDigits_WhileTyping()
    {
        // UAT: ô "Thuế khác (VNĐ)" hiện 200000 thay vì 200.000.
        var page = await SignedInPageAsync();
        await page.GotoAsync(BaseUrl + "/purchasing?form=1");

        var input = page.Locator("input[name='Form.Lines[0].SupplementalDuty']");
        await input.FillAsync(string.Empty);
        await input.PressSequentiallyAsync("200000");
        await input.BlurAsync();

        Assert.Equal("200.000", await input.InputValueAsync());
        Assert.Equal("200000", await input.EvaluateAsync<string>("element => new FormData(element.form).get(element.name)"));
        AssertNoScriptErrors();
    }

    [Fact]
    public async Task CatalogPriceInput_GroupsWhileTyping_AndPostsTheRawNumber()
    {
        // Một script ô tiền cho mọi trang: Danh mục trước đây chỉ nhóm khi rời ô và thêm chữ " VNĐ" vào ô.
        var page = await SignedInPageAsync();
        await page.GotoAsync(BaseUrl + "/catalog?add=true");

        var input = page.Locator("#Form_CostPrice");
        await input.FillAsync(string.Empty);
        await input.PressSequentiallyAsync("1500000");

        Assert.Equal("1.500.000", await input.InputValueAsync());
        var posted = await input.EvaluateAsync<string>("element => new FormData(element.form).get('Form.CostPrice')");
        Assert.Equal("1500000", posted);
        Assert.Equal("1.500.000", await input.InputValueAsync());
        AssertNoScriptErrors();
    }

    [Fact]
    public async Task Header_StaysAtTheTop_WhenThePageScrolls()
    {
        var page = await SignedInPageAsync(height: 600);
        await page.GotoAsync(BaseUrl + "/purchasing");
        await page.EvaluateAsync("() => { document.querySelector('main').style.minHeight = '3000px'; window.scrollTo(0, 1500); }");

        var top = await page.EvaluateAsync<double>("() => document.querySelector('#app-content > header').getBoundingClientRect().top");
        Assert.Equal(0, top, 0.5);
        Assert.True(await page.Locator("#app-content > header").IsVisibleAsync());
        AssertNoScriptErrors();
    }

    [Theory]
    [InlineData(1920)]
    [InlineData(1280)]
    public async Task CollapsedMenu_KeepsTheContentCentered(int width)
    {
        // UAT: thu nhỏ menu thì nội dung dính trái, hở khoảng trống bên phải.
        var page = await SignedInPageAsync(width: width);
        await page.GotoAsync(BaseUrl + "/purchasing");
        await page.ClickAsync("[data-sidebar-toggle]");
        await page.WaitForTimeoutAsync(400);

        var gaps = await page.EvaluateAsync<double[]>(
            "() => { const outer = document.getElementById('app-content').getBoundingClientRect(); const main = document.querySelector('main').getBoundingClientRect(); return [main.left - outer.left, outer.right - main.right]; }");
        Assert.Equal(gaps[0], gaps[1], 2.0);
        AssertNoScriptErrors();
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(1024)]
    public async Task ReceiveButtonAndCloseCheckbox_StayOnOneRow_ButtonFirst(int width)
    {
        var orders = Factory.Services.GetRequiredService<PurchaseDirectoryDouble>();
        var catalog = Factory.Services.GetRequiredService<CatalogDirectoryDouble>();
        var item = CatalogItem.Create("SMOKE01", "Bồn", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", null, null, null, null, 9m, 1m, 1m, 1m, 1m);
        catalog.Items.Add(item);
        var order = PurchaseOrder.TryCreate("PO-SMOKE-" + width, Guid.CreateVersion7(), Guid.CreateVersion7(), "CONT", "BL", "Hải Phòng", null, 25000m, 0.1m, [(item.Id, 4, 10m, 0m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        orders.Orders.Add(order);

        var page = await SignedInPageAsync(width: width);
        await page.GotoAsync(BaseUrl + "/purchasing?order=" + order.Id);

        var button = await page.Locator("#receive-form button[type='submit']").BoundingBoxAsync();
        var checkbox = await page.Locator("#receive-form label").BoundingBoxAsync();
        Assert.NotNull(button);
        Assert.NotNull(checkbox);
        Assert.True(checkbox.X > button.X + button.Width - 1, "Ô đóng đơn phải đứng sau nút nhập kho.");
        Assert.Equal(button.Y + (button.Height / 2), checkbox.Y + (checkbox.Height / 2), 4.0);
        AssertNoScriptErrors();
    }

    [Fact]
    public async Task ApprovingAnOrderFromAFilteredList_KeepsTheFilters()
    {
        // UAT: duyệt, nhận hàng tải lại trang mà mất bộ lọc. Đi trọn luồng thật: lọc, chọn đơn, bấm, xác nhận.
        var orders = Factory.Services.GetRequiredService<PurchaseDirectoryDouble>();
        var order = PurchaseOrder.TryCreate("PO-SMOKE-KEEP", Guid.CreateVersion7(), Guid.CreateVersion7(), "CONT", "BL", "Hải Phòng", null, 25000m, 0.1m, [(Guid.CreateVersion7(), 4, 10m, 0m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        orders.Orders.Add(order);

        var page = await SignedInPageAsync();
        await page.GotoAsync(BaseUrl + "/purchasing?q=PO-SMOKE-KEEP&status=PendingApproval&size=50&order=" + order.Id);
        await page.ClickAsync("#order-workflow button:has-text('Duyệt đơn')");
        await page.ClickAsync("#confirm-ok");
        await page.WaitForURLAsync(url => url.Contains("notice=approved", StringComparison.Ordinal));

        Assert.Contains("q=PO-SMOKE-KEEP", page.Url, StringComparison.Ordinal);
        Assert.Contains("status=PendingApproval", page.Url, StringComparison.Ordinal);
        Assert.Contains("size=50", page.Url, StringComparison.Ordinal);
        Assert.Equal(PurchaseStatus.Approved, order.Status);
        AssertNoScriptErrors();
    }

    [Fact]
    public async Task ClosingThePurchaseFormWithEscape_KeepsTheFiltersInTheAddress()
    {
        // Rà toàn bộ (2026-11-21): Esc đóng form lập đơn rồi ghi lại địa chỉ thành /purchasing trần, tải lại trang là mất bộ lọc.
        var page = await SignedInPageAsync();
        await page.GotoAsync(BaseUrl + "/purchasing?form=1&q=PO&status=Draft&size=50");
        await page.Locator("#purchase-dialog").WaitForAsync();

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync("() => !location.search.includes('form=1')");

        var query = new Uri(page.Url).Query;
        Assert.Contains("q=PO", query, StringComparison.Ordinal);
        Assert.Contains("status=Draft", query, StringComparison.Ordinal);
        Assert.Contains("size=50", query, StringComparison.Ordinal);
        AssertNoScriptErrors();
    }

    [Fact]
    public async Task RejectedStocktake_ShowsItsErrorInsideTheReopenedDialog()
    {
        // UAT: lỗi lập phiếu kiểm kê hiện sau lớp phủ của hộp thoại, người dùng không thấy.
        var page = await SignedInPageAsync();
        await page.GotoAsync(BaseUrl + "/inventory?view=stocktakes");
        await page.ClickAsync("#open-stocktake");
        await page.FillAsync("#stocktake-dialog input[name='Stocktake.CountedQuantity']", "3");
        await page.FillAsync("#stocktake-dialog input[name='Stocktake.Reason']", "đếm lại");
        await page.ClickAsync("#stocktake-dialog button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var dialog = page.Locator("#stocktake-dialog");
        Assert.True(await dialog.EvaluateAsync<bool>("element => element.open"), "Hộp thoại phải mở lại.");
        Assert.True(await dialog.Locator("[role='alert']").IsVisibleAsync(), "Câu lỗi phải nằm trong hộp thoại.");
        Assert.Equal("đếm lại", await dialog.Locator("input[name='Stocktake.Reason']").InputValueAsync());
        AssertNoScriptErrors();
    }
}
