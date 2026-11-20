using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>
/// Tab Kiểm kê và điều chỉnh: đếm, cắt đoạn, định vị phiếu trong PostgreSQL theo phạm vi kho; và nhãn phiếu cho lịch sử tồn
/// chỉ cho phiếu người dùng được thấy.
/// </summary>
public class StockDocumentListTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    private Guid _a;
    private Guid _b;
    private Guid _item;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _a = await SeedLocationAsync("Kho A");
        _b = await SeedLocationAsync("Kho B");
        _item = (await SeedItemsAsync(1))[0];
    }

    private async Task<StocktakeSheet> AddStocktakeAsync(Guid location, string reason = "đếm lại")
    {
        Assert.Equal(StockDecision.Accepted, StocktakeSheet.TryCreate(EmployeeRole.WarehouseKeeper, location, _item, 5, 4, reason, "BB-01", null, out var sheet));
        await using var context = Database.NewContext();
        context.StocktakeSheets.Add(sheet!);
        await context.SaveChangesAsync();
        return sheet!;
    }

    private async Task<QuantityAdjustment> AddAdjustmentAsync(Guid location)
    {
        var adjustment = QuantityAdjustment.TryRecordTransferVariance(Guid.CreateVersion7(), location, _item, -1, "thiếu", "CK-000001")!;
        await using var context = Database.NewContext();
        context.QuantityAdjustments.Add(adjustment);
        await context.SaveChangesAsync();
        return adjustment;
    }

    private async Task<DisposalSlip> AddDisposalAsync(Guid location)
    {
        Assert.Equal(StockDecision.Accepted, DisposalSlip.TryCreate(location, _item, 1, "vỡ", "BB-02", null, EmployeeRole.WarehouseKeeper, out var slip));
        await using var context = Database.NewContext();
        context.DisposalSlips.Add(slip!);
        await context.SaveChangesAsync();
        return slip!;
    }

    private static InventoryRepository Repository(PancrasDbContext context) => new(context, NullLogger<InventoryRepository>.Instance);

    private static ListInventoryDocumentsHandler Handler(PancrasDbContext context) =>
        new(Repository(context), NullLogger<ListInventoryDocumentsHandler>.Instance);

    [Fact]
    public async Task Counts_AndRanges_FollowTheLocationScope_NewestFirst()
    {
        var first = await AddStocktakeAsync(_a);
        var second = await AddStocktakeAsync(_a);
        var third = await AddStocktakeAsync(_a);
        await AddStocktakeAsync(_b);
        await AddAdjustmentAsync(_a);
        await AddDisposalAsync(_b);

        await using var context = Database.NewContext();
        var repository = Repository(context);

        Assert.Equal(new StockDocumentCounts(4, 1, 1), await repository.CountStockDocumentsAsync(null, CancellationToken.None));
        Assert.Equal(new StockDocumentCounts(3, 1, 0), await repository.CountStockDocumentsAsync(_a, CancellationToken.None));
        Assert.Equal(new StockDocumentCounts(0, 0, 0), await repository.CountStockDocumentsAsync(Guid.Empty, CancellationToken.None));
        var slice = await repository.ListStocktakesAsync(_a, new StockDocumentRange(1, 2), CancellationToken.None);
        Assert.Equal([second.Id, first.Id], slice.Select(sheet => sheet.Id).ToArray());
        Assert.Empty(await repository.ListStocktakesAsync(_a, new StockDocumentRange(0, 0), CancellationToken.None));
        Assert.Equal(third.Id, (await repository.ListStocktakesAsync(_a, new StockDocumentRange(0, 1), CancellationToken.None)).Single().Id);
    }

    [Fact]
    public async Task Locate_GivesKindAndIndexWithinTheScope_OrNothing()
    {
        var older = await AddAdjustmentAsync(_a);
        var newer = await AddAdjustmentAsync(_a);
        var elsewhere = await AddDisposalAsync(_b);

        await using var context = Database.NewContext();
        var repository = Repository(context);

        Assert.Equal(new StockDocumentPosition(StockDocumentKind.Adjustment, 1), await repository.LocateStockDocumentAsync(_a, older.Id, CancellationToken.None));
        Assert.Equal(new StockDocumentPosition(StockDocumentKind.Adjustment, 0), await repository.LocateStockDocumentAsync(null, newer.Id, CancellationToken.None));
        Assert.Null(await repository.LocateStockDocumentAsync(_a, elsewhere.Id, CancellationToken.None));
        Assert.Equal(new StockDocumentPosition(StockDocumentKind.Disposal, 0), await repository.LocateStockDocumentAsync(null, elsewhere.Id, CancellationToken.None));
    }

    [Fact]
    public async Task PageWithFocus_OpensThePageHoldingTheDocument_ForTheRightPeople()
    {
        for (var i = 0; i < 3; i++)
        {
            await AddStocktakeAsync(_a);
        }

        var target = await AddDisposalAsync(_a);
        var keeperA = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _a);
        var keeperB = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _b);
        var sales = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.Sales, _a);

        await using var context = Database.NewContext();
        var handler = Handler(context);
        var focused = await handler.ListStockDocumentPageAsync(keeperA, 1, 20, target.Id, CancellationToken.None);
        var small = await handler.ListStockDocumentPageAsync(keeperA, 1, 1, target.Id, CancellationToken.None);
        var other = await handler.ListStockDocumentPageAsync(keeperB, 1, 20, target.Id, CancellationToken.None);
        var none = await handler.ListStockDocumentPageAsync(sales, 1, 20, null, CancellationToken.None);

        Assert.Equal(4, focused.Total);
        Assert.Equal(3, focused.Stocktakes.Count);
        Assert.Equal(target.Id, Assert.Single(focused.Disposals).Id);
        Assert.Equal(1, focused.Page);
        // Cỡ trang không hợp lệ (1) được đưa về 20 như mọi danh sách.
        Assert.Equal(1, small.Page);
        Assert.Equal(0, other.Total);
        Assert.Equal(0, none.Total);
    }

    [Fact]
    public async Task DescribeDocuments_LabelsOnlyDocumentsTheActorMaySee()
    {
        var stocktake = await AddStocktakeAsync(_a, "lệch số");
        var hidden = await AddStocktakeAsync(_b, "kho khác");
        var slip = TransferSlip.Create(_a, _b, "chuyển gấp", [(_item, 1)])!;
        await using (var seed = Database.NewContext())
        {
            seed.TransferSlips.Add(slip);
            await seed.SaveChangesAsync();
        }

        var keeperA = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _a);
        await using var context = Database.NewContext();
        var labels = await Handler(context).DescribeDocumentsAsync(keeperA, [stocktake.Id, hidden.Id, slip.Id, Guid.Empty], CancellationToken.None);

        Assert.Equal(2, labels.Count);
        Assert.Equal(StockDocumentLabelKind.Stocktake, labels[stocktake.Id].Kind);
        Assert.Equal("lệch số", labels[stocktake.Id].Text);
        var transfer = labels[slip.Id];
        Assert.Equal(StockDocumentLabelKind.Transfer, transfer.Kind);
        Assert.True(transfer.TransferNumber > 0);
        Assert.Equal("Kho A", transfer.SourceName);
        Assert.Equal("Kho B", transfer.DestinationName);
        Assert.Equal("chuyển gấp", transfer.Text);
    }
}
