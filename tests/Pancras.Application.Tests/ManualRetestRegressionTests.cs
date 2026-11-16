using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

/// <summary>Các lỗi còn lại sau lần chạy lại test thủ công 15/11/2026.</summary>
public class ManualRetestRegressionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DirectAdjustment_WithoutLocationOrItem_IsRejected_NotThrown(bool missingLocation)
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        store.Locations.Add(location);
        var handler = new CreateQuantityAdjustmentCommandHandler(store, new FakeEmployeeRepository(), NullLogger<CreateQuantityAdjustmentCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new CreateQuantityAdjustmentCommand
            {
                Actor = TestActors.Accountant,
                StockLocationId = missingLocation ? Guid.Empty : location.Id,
                CatalogItemId = missingLocation ? Guid.CreateVersion7() : Guid.Empty,
                Delta = 2,
                Reason = "Kiểm lại",
                DocumentRef = "DC-01"
            },
            CancellationToken.None);

        Assert.Equal(StockError.MissingTarget, result.Error);
    }

    [Fact]
    public async Task Update_RejectsAMissingKind()
    {
        var repo = new LocationFake();
        var location = StockLocation.Create("Showroom 1", StockLocationKind.Showroom);
        repo.Locations.Add(location);

        var result = await new UpdateLocationCommandHandler(repo, NullLogger<UpdateLocationCommandHandler>.Instance)
            .HandleAsync(new UpdateLocationCommand { Id = location.Id, Name = "Showroom 1", Kind = null }, TestActors.Admin, CancellationToken.None);

        Assert.Equal(LocationManageError.InvalidKind, result);
        Assert.Equal(StockLocationKind.Showroom, location.Kind);
    }

    [Fact]
    public void FitsLimits_AcceptsAnOrdinaryOrder_AndRejectsNumbersThatWouldOverflowAColumn()
    {
        (decimal, decimal, decimal, decimal, decimal) Line(decimal fob = 1000m, decimal duty = 0.1m, decimal supplemental = 0m) =>
            (fob, duty, supplemental, 0m, 0m);

        Assert.True(PurchaseMoney.FitsLimits(25_000m, 0.1m, [Line()], [1_000_000m]));
        Assert.False(PurchaseMoney.FitsLimits(25_000m, 0.1m, [Line(fob: 1e16m)], []));
        Assert.False(PurchaseMoney.FitsLimits(25_000m, 0.1m, [Line(duty: 1e6m)], []));
        Assert.False(PurchaseMoney.FitsLimits(25_000m, 1e5m, [Line()], []));
        Assert.False(PurchaseMoney.FitsLimits(25_000m, 0.1m, [Line(supplemental: 1e18m)], []));
        Assert.False(PurchaseMoney.FitsLimits(25_000m, 0.1m, [Line()], [1e18m]));
        Assert.False(PurchaseMoney.FitsLimits(1e20m, 0.1m, [Line()], []));
        Assert.False(PurchaseMoney.FitsLimits(1e15m, 0.1m, [Line(fob: 1e12m)], []));
        Assert.False(PurchaseMoney.FitsLimits(decimal.MaxValue, 0.1m, [Line(fob: decimal.MaxValue)], []));
    }

    [Fact]
    public async Task SecondApprovalOfAnAdjustment_SaysItWasAlreadyHandled_NotThatStockIsShort()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        store.Locations.Add(location);
        var itemId = Guid.CreateVersion7();
        store.Balances.Add(StockBalance.Create(itemId, location.Id, 5, 0, 0));
        var created = await new CreateQuantityAdjustmentCommandHandler(store, new FakeEmployeeRepository(), NullLogger<CreateQuantityAdjustmentCommandHandler>.Instance)
            .HandleAsync(
                new CreateQuantityAdjustmentCommand
                {
                    Actor = TestActors.Keeper(location.Id),
                    StockLocationId = location.Id,
                    CatalogItemId = itemId,
                    Delta = 1,
                    Reason = "Thừa",
                    DocumentRef = "DC-02"
                },
                CancellationToken.None);
        var approve = new ApproveQuantityAdjustmentCommandHandler(store, NullLogger<ApproveQuantityAdjustmentCommandHandler>.Instance);

        var first = await approve.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Director), CancellationToken.None);
        var second = await approve.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Director), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(StockError.TransferStateChanged, second.Error);
    }

    private sealed class LocationFake : ILocationManagementRepository
    {
        public List<StockLocation> Locations { get; } = [];

        public Task<StockLocation?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Locations.FirstOrDefault(l => l.Id == id));

        public Task<bool> NameExistsAsync(string name, Guid excludingId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasActiveEmployeesAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasOpenDocumentsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> TrySaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
