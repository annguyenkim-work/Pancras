using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

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
    public async Task ShipTransfer_ShipsOnce_AndRedirectsWithoutAnError()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var inventory = Reset();
        var source = Guid.CreateVersion7();
        var destination = Guid.CreateVersion7();
        var item = Guid.CreateVersion7();
        var here = StockBalance.Create(item, source, 10, 0, 0);
        inventory.Balances.Add(here);
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var page = await client.GetAsync("/inventory?view=transfers");
        var token = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync(
            "/inventory?handler=ShipTransfer",
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
        Assert.Equal(TransferStatus.InTransit, slip.Status);
        Assert.Equal(7, here.OnHand);
        Assert.Equal(3, inventory.Balances.Single(balance => balance.StockLocationId == destination).InTransit);
        Assert.Single(inventory.Movements);
    }

    private static StockBalanceRow Row(string code, string name, ProductCategory category, Guid locationId) =>
        new(Guid.CreateVersion7(), code, name, "Toto", category, locationId, "Kho A", 3, 1, 2, 4);

    private InventoryDirectoryDouble Reset()
    {
        var inventory = _factory.Services.GetRequiredService<InventoryDirectoryDouble>();
        inventory.Rows.Clear();
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

internal sealed class InventoryDirectoryDouble : IInventoryRepository
{
    public List<StockBalanceRow> Rows { get; } = [];

    public List<StockBalance> Balances { get; } = [];

    public List<TransferSlip> Transfers { get; } = [];

    public List<StockMovement> Movements { get; } = [];

    public Task<IReadOnlyList<StockLocation>> ListLocationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockLocation>>([]);

    public Task<IReadOnlyList<StockItemChoice>> ListItemChoicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockItemChoice>>([]);

    public Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(StockListFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockBalanceRow>>(Rows.Where(filter.Matches).ToArray());

    public Task<int> SumOnHandAsync(Guid catalogItemId, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<StockBalance?> FindBalanceForUpdateAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken) =>
        Task.FromResult(Balances.FirstOrDefault(balance => balance.CatalogItemId == catalogItemId && balance.StockLocationId == stockLocationId));

    public Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken)
    {
        Balances.Add(balance);
        return Task.CompletedTask;
    }

    public Task AddTransferAsync(TransferSlip slip, CancellationToken cancellationToken)
    {
        Transfers.Add(slip);
        return Task.CompletedTask;
    }

    public Task<TransferSlip?> FindTransferForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<TransferSlip?>(null);

    public Task<IReadOnlyList<TransferSlip>> ListOpenTransfersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferSlip>>([]);

    public Task<IReadOnlyList<TransferSlip>> ListTransfersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferSlip>>([]);

    public Task<StockItemProfile?> FindItemProfileAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<StockItemProfile?>(null);

    public Task<IReadOnlyList<StockBalanceRow>> ListItemBalancesAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockBalanceRow>>(Rows.Where(row => row.CatalogItemId == catalogItemId).ToArray());

    public Task<IReadOnlyList<StockMovement>> ListItemMovementsAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockMovement>>([]);

    public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StockMovement>> ListHistoryAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockMovement>>([]);

    public Task AddStocktakeAsync(StocktakeSheet sheet, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<StocktakeSheet?> FindStocktakeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<StocktakeSheet?>(null);

    public Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StocktakeSheet>>([]);

    public Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<QuantityAdjustment?>(null);

    public Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuantityAdjustment>>([]);

    public Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<DisposalSlip?>(null);

    public Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DisposalSlip>>([]);

    public Task AddNoticeAsync(StockNotice notice, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<StockNotice>> ListNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockNotice>>([]);

    public Task<StockNotice?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<StockNotice?>(null);

    public Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> CommitAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
