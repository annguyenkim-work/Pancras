using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Danh sách phiếu chuyển cắt trang, lọc, đếm trong PostgreSQL; quyền xem và nhóm "chờ tôi" lấy từ <see cref="TransferWorkflowAccess"/>.</summary>
public class TransferListPageTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    private static readonly EmployeeActor Finance = new(Guid.CreateVersion7(), EmployeeRole.Accountant, null);

    private Guid _a;
    private Guid _b;
    private Guid _c;
    private Guid _faucet;
    private Guid _basin;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _a = await SeedLocationAsync("Kho A");
        _b = await SeedLocationAsync("Kho B");
        _c = await SeedLocationAsync("Kho C");
        await using var context = Database.NewContext();
        var faucet = Pancras.Domain.Catalog.CatalogItem.Create("VOI-01", "Vòi sen nóng lạnh", Pancras.Domain.Catalog.ProductCategory.SanitaryEquipment, "Toto", "Nhật", "Cái", 1m, null, null, null, 1m, 1m, 1m, 1m, 1m);
        var basin = Pancras.Domain.Catalog.CatalogItem.Create("CHAU-02", "Chậu rửa", Pancras.Domain.Catalog.ProductCategory.SanitaryEquipment, "Toto", "Nhật", "Cái", 1m, null, null, null, 1m, 1m, 1m, 1m, 1m);
        context.CatalogItems.AddRange(faucet, basin);
        await context.SaveChangesAsync();
        _faucet = faucet.Id;
        _basin = basin.Id;
    }

    private async Task<TransferSlip> AddAsync(Guid source, Guid destination, TransferStatus status = TransferStatus.Requested, Guid? item = null)
    {
        var slip = TransferSlip.Create(source, destination, "ghi chú", [(item ?? _faucet, 1)])!;
        typeof(TransferSlip).GetProperty(nameof(TransferSlip.Status))!.SetValue(slip, status);
        await using var context = Database.NewContext();
        context.TransferSlips.Add(slip);
        await context.SaveChangesAsync();
        return slip;
    }

    private async Task<TransferPage> PageAsync(EmployeeActor actor, Func<TransferListQuery, TransferListQuery>? refine = null, int page = 1, int size = 20)
    {
        await using var context = Database.NewContext();
        var query = new TransferListQuery(TransferWorkflowAccess.Visible(actor));
        return await Repository(context).ListTransfersPageAsync(refine?.Invoke(query) ?? query, page, size, CancellationToken.None);
    }

    private static InventoryRepository Repository(PancrasDbContext context) => new(context, NullLogger<InventoryRepository>.Instance);

    [Fact]
    public async Task Page_ListsOpenSlipsFirst_ThenNewestFirst_AndCountsTheWholeList()
    {
        var done = await AddAsync(_a, _b, TransferStatus.Received);
        var requested = await AddAsync(_a, _b, TransferStatus.Requested);
        var cancelled = await AddAsync(_a, _b, TransferStatus.Cancelled);
        var inTransit = await AddAsync(_a, _b, TransferStatus.InTransit);

        var all = await PageAsync(Finance);
        var second = await PageAsync(Finance, page: 2, size: 3);
        var beyond = await PageAsync(Finance, page: 9, size: 3);

        Assert.Equal(4, all.Total);
        Assert.Equal([inTransit.Id, requested.Id, cancelled.Id, done.Id], all.Items.Select(slip => slip.Id).ToArray());
        Assert.Equal([done.Id], second.Items.Select(slip => slip.Id).ToArray());
        Assert.Equal(2, beyond.PageNumber);
        Assert.All(all.Items, slip => Assert.NotEmpty(slip.Lines));
    }

    [Fact]
    public async Task Page_AppliesTheViewRule_ToRowsTotalAndAnyVisible()
    {
        await AddAsync(_a, _b);
        await AddAsync(_b, _c);
        await AddAsync(_c, _b);
        var keeperA = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _a);
        var keeperB = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _b);
        var sales = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.Sales, _a);
        var unassigned = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, null);

        Assert.Equal(3, (await PageAsync(Finance)).Total);
        Assert.Equal(1, (await PageAsync(keeperA)).Total);
        Assert.Equal(3, (await PageAsync(keeperB)).Total);
        var none = await PageAsync(sales);
        Assert.Equal(0, none.Total);
        Assert.False(none.AnyVisible);
        Assert.False((await PageAsync(unassigned)).AnyVisible);
        var filteredOut = await PageAsync(keeperA, query => query with { Term = "không-có" });
        Assert.Equal(0, filteredOut.Total);
        Assert.True(filteredOut.AnyVisible);
    }

    [Theory]
    [InlineData(TransferWaitingKind.Decide)]
    [InlineData(TransferWaitingKind.Ship)]
    [InlineData(TransferWaitingKind.Receive)]
    [InlineData(TransferWaitingKind.Resolve)]
    [InlineData(TransferWaitingKind.Any)]
    public async Task Waiting_SelectsExactlyTheSlipsTheActorMustHandle_AndCountsThemTheSame(TransferWaitingKind kind)
    {
        foreach (var status in new[] { TransferStatus.Requested, TransferStatus.Approved, TransferStatus.InTransit, TransferStatus.ReceivedWithDiscrepancy, TransferStatus.Received })
        {
            await AddAsync(_a, _b, status);
            await AddAsync(_b, _a, status);
            await AddAsync(_b, _c, status);
        }

        var actors = new[]
        {
            Finance,
            new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _a),
            new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.ShowroomWarehouseLead, _a),
            new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.Sales, _a)
        };
        foreach (var actor in actors)
        {
            var visible = TransferWorkflowAccess.Visible(actor);
            var waiting = TransferWorkflowAccess.Waiting(actor, kind);
            var page = await PageAsync(actor, query => query with { Waiting = waiting });

            await using var context = Database.NewContext();
            var everything = await context.TransferSlips.AsNoTracking().ToListAsync();
            var expected = everything
                .Where(slip => visible.Compile()(slip)
                    && TransferWorkflowAccess.IsWaiting(actor, kind, slip.Status, slip.SourceLocationId, slip.DestinationLocationId))
                .Select(slip => slip.Id)
                .Order()
                .ToArray();

            Assert.Equal(expected, page.Items.Select(slip => slip.Id).Order().ToArray());
            Assert.Equal(expected.Length, page.Total);
            Assert.Equal(expected.Length, await Repository(context).CountTransfersAsync(Pancras.Application.Common.Predicates.And(visible, waiting), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Page_FiltersByStatusLocationAndText()
    {
        var faucetRequest = await AddAsync(_a, _b, TransferStatus.Requested, _faucet);
        var basinApproved = await AddAsync(_b, _c, TransferStatus.Approved, _basin);

        Assert.Equal(basinApproved.Id, Assert.Single((await PageAsync(Finance, q => q with { Status = TransferStatus.Approved })).Items).Id);
        Assert.Equal(faucetRequest.Id, Assert.Single((await PageAsync(Finance, q => q with { LocationId = _a })).Items).Id);
        Assert.Equal(2, (await PageAsync(Finance, q => q with { LocationId = _b })).Total);
        Assert.Equal(faucetRequest.Id, Assert.Single((await PageAsync(Finance, q => q with { Term = "voi-01" })).Items).Id);
        Assert.Equal(basinApproved.Id, Assert.Single((await PageAsync(Finance, q => q with { Term = "chậu" })).Items).Id);
        var code = StockDocumentCode.ForTransfer(faucetRequest.Number == 0 ? (await ReloadNumberAsync(faucetRequest.Id)) : faucetRequest.Number);
        Assert.Equal(faucetRequest.Id, Assert.Single((await PageAsync(Finance, q => q with { Term = code.ToLowerInvariant() })).Items).Id);
        Assert.Empty((await PageAsync(Finance, q => q with { Term = "%" })).Items);
    }

    [Fact]
    public async Task Page_FiltersByTheCreationTimeCarriedInTheId()
    {
        var slip = await AddAsync(_a, _b);
        var now = DateTimeOffset.UtcNow;

        Assert.Single((await PageAsync(Finance, q => q with { CreatedFrom = now.AddMinutes(-5) })).Items);
        Assert.Empty((await PageAsync(Finance, q => q with { CreatedFrom = now.AddMinutes(5) })).Items);
        Assert.Empty((await PageAsync(Finance, q => q with { CreatedBefore = now.AddMinutes(-5) })).Items);
        Assert.Single((await PageAsync(Finance, q => q with { CreatedBefore = now.AddMinutes(5) })).Items);
        Assert.Equal(slip.Id, Assert.Single((await PageAsync(Finance, q => q with { CreatedFrom = now.AddMinutes(-5), CreatedBefore = now.AddMinutes(5) })).Items).Id);
    }

    [Fact]
    public async Task FindTransfers_ReturnsOnlyTheRequestedSlipsTheActorMaySee()
    {
        var mine = await AddAsync(_a, _b);
        var elsewhere = await AddAsync(_b, _c);
        var keeperA = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _a);

        await using var context = Database.NewContext();
        var found = await Repository(context).FindTransfersAsync(TransferWorkflowAccess.Visible(keeperA), [mine.Id, elsewhere.Id], CancellationToken.None);

        Assert.Equal(mine.Id, Assert.Single(found).Id);
        Assert.NotEmpty(found[0].Lines);
    }

    private async Task<int> ReloadNumberAsync(Guid id)
    {
        await using var context = Database.NewContext();
        return (await context.TransferSlips.AsNoTracking().SingleAsync(slip => slip.Id == id)).Number;
    }
}
