using Pancras.Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class DisposalSlipHandlerTests
{
    [Fact]
    public async Task Adjustment_RejectsWhenOnHandWouldFallBelowReserved()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var itemId = Guid.CreateVersion7();
        var balance = StockBalance.Create(itemId, location.Id, 5, 4, 0);
        store.Balances.Add(balance);
        var created = await new CreateQuantityAdjustmentCommandHandler(store, store, new FakeEmployeeRepository(), NullLogger<CreateQuantityAdjustmentCommandHandler>.Instance)
            .HandleAsync(new CreateQuantityAdjustmentCommand
            {
                Actor = TestActors.Keeper(location.Id),
                StockLocationId = location.Id,
                CatalogItemId = itemId,
                Delta = -2,
                Reason = "Hao hụt",
                DocumentRef = "DC-01"
            }, CancellationToken.None);
        var result = await new ApproveQuantityAdjustmentCommandHandler(store, store, NullLogger<ApproveQuantityAdjustmentCommandHandler>.Instance)
            .HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Accountant), CancellationToken.None);

        Assert.Equal(StockError.NegativeStock, result.Error);
        Assert.Equal(5, balance.OnHand);
        Assert.Equal(4, balance.Reserved);
        Assert.Equal(ApprovalStatus.Pending, store.Adjustments[0].Status);
    }

    [Theory]
    [InlineData(EmployeeRole.ShowroomWarehouseLead)]
    [InlineData(EmployeeRole.Director)]
    public async Task Approve_ByLeadOrDirector_DropsAvailableByOne_AndWritesOneNotice(string role)
    {
        var store = Prepared(onHand: 5, reserved: 0);
        var created = await CreateAsync(store);
        var result = await ApproveAsync(store, created.Value, role);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, store.Balance.Available);
        Assert.Equal(4, store.Balance.OnHand);
        Assert.Equal(0, store.Balance.Reserved);
        var notice = Assert.Single(store.Notices);
        Assert.Equal(store.Location.Id, notice.StockLocationId);
        Assert.Equal(NotificationKind.DisposalApproved, notice.Kind);
        Assert.Equal(store.Keeper.Id, notice.RecipientEmployeeId);
        Assert.Equal(Notification.DisposalStep, notice.Step);
    }

    [Fact]
    public async Task Approve_DropsTheWholeQuantity()
    {
        var store = Prepared(onHand: 5, reserved: 1);
        var created = await CreateAsync(store, quantity: 3);

        var result = await ApproveAsync(store, created.Value, EmployeeRole.ShowroomWarehouseLead);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, store.Balance.OnHand);
        Assert.Equal(3, store.Disposals[0].Quantity);
    }

    [Fact]
    public async Task Approve_RejectsAQuantityAboveTheAvailableStock()
    {
        var store = Prepared(onHand: 5, reserved: 3);
        var created = await CreateAsync(store, quantity: 3);

        var result = await ApproveAsync(store, created.Value, EmployeeRole.ShowroomWarehouseLead);

        Assert.Equal(StockError.DisposalRejected, result.Error);
        Assert.Equal(5, store.Balance.OnHand);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Create_RejectsAQuantityBelowOne(int quantity)
    {
        var store = Prepared(onHand: 5, reserved: 0);

        var result = await CreateAsync(store, quantity);

        Assert.Equal(StockError.InvalidQuantity, result.Error);
        Assert.Empty(store.Disposals);
    }

    [Fact]
    public async Task Approve_OtherRole_LeavesAvailableUnchanged()
    {
        var store = Prepared(onHand: 5, reserved: 0);
        var created = await CreateAsync(store);

        var result = await ApproveAsync(store, created.Value, EmployeeRole.Sales);

        Assert.Equal(StockError.ApprovalForbidden, result.Error);
        Assert.Equal(5, store.Balance.Available);
        Assert.Equal(ApprovalStatus.Pending, store.Disposals[0].Status);
        Assert.Empty(store.Notices);
    }

    [Fact]
    public async Task Approve_SecondTime_DoesNotSubtractAgain()
    {
        var store = Prepared(onHand: 5, reserved: 0);
        var created = await CreateAsync(store);
        await ApproveAsync(store, created.Value, EmployeeRole.Director);

        var second = await ApproveAsync(store, created.Value, EmployeeRole.Director);

        Assert.Equal(StockError.TransferStateChanged, second.Error);
        Assert.Equal(4, store.Balance.Available);
        Assert.Single(store.Notices);
    }

    [Fact]
    public async Task Approve_RejectsWhenAvailableIsZero()
    {
        var store = Prepared(onHand: 0, reserved: 0);
        var created = await CreateAsync(store);

        var result = await ApproveAsync(store, created.Value, EmployeeRole.ShowroomWarehouseLead);

        Assert.Equal(StockError.DisposalRejected, result.Error);
        Assert.Equal(0, store.Balance.OnHand);
        Assert.Equal(0, store.Balance.Available);
        Assert.Empty(store.Notices);
    }

    [Fact]
    public async Task Finance_PostsTheAdjustmentImmediately_AndKeeperDraftNotifiesFinance()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var itemId = Guid.CreateVersion7();
        var balance = StockBalance.Create(itemId, location.Id, 5, 0, 0);
        store.Locations.Add(location);
        store.Balances.Add(balance);
        var people = new FakeEmployeeRepository();
        var accountant = Employee.Create("Kế Toán", "KeToan", "KETOAN", "hash", EmployeeRole.Accountant);
        people.Employees.Add(accountant);
        var handler = new CreateQuantityAdjustmentCommandHandler(store, store, people, NullLogger<CreateQuantityAdjustmentCommandHandler>.Instance);

        var direct = await handler.HandleAsync(new CreateQuantityAdjustmentCommand
        {
            Actor = TestActors.Accountant,
            StockLocationId = location.Id,
            CatalogItemId = itemId,
            Delta = 2,
            Reason = "Kiểm lại",
            DocumentRef = "DC-02"
        }, CancellationToken.None);

        Assert.True(direct.IsSuccess);
        Assert.Equal(7, balance.OnHand);
        Assert.Equal(ApprovalStatus.Approved, store.Adjustments[0].Status);
        Assert.Empty(store.Notices);

        var drafted = await handler.HandleAsync(new CreateQuantityAdjustmentCommand
        {
            Actor = TestActors.Keeper(location.Id),
            StockLocationId = location.Id,
            CatalogItemId = itemId,
            Delta = -1,
            Reason = "Hao hụt",
            DocumentRef = "DC-03"
        }, CancellationToken.None);

        Assert.True(drafted.IsSuccess);
        Assert.Equal(7, balance.OnHand);
        Assert.Equal(ApprovalStatus.Pending, store.Adjustments[1].Status);
        var notice = Assert.Single(store.Notices);
        Assert.Equal(accountant.Id, notice.RecipientEmployeeId);
        Assert.Equal(NotificationKind.AdjustmentPending, notice.Kind);
    }

    private static Task<Result<Guid, StockError>> CreateAsync(DisposalFixture store, int quantity = 1) =>
        new CreateDisposalSlipCommandHandler(
                store.Store,
                store.Store,
                new ApproveDisposalSlipCommandHandler(store.Store, store.Store, store.Employees, NullLogger<ApproveDisposalSlipCommandHandler>.Instance),
                NullLogger<CreateDisposalSlipCommandHandler>.Instance)
            .HandleAsync(new CreateDisposalSlipCommand
            {
                Actor = TestActors.Keeper(store.Location.Id),
                StockLocationId = store.Location.Id,
                CatalogItemId = store.ItemId,
                Reason = "Vỡ",
                DocumentRef = "HH-01",
                Quantity = quantity
            }, CancellationToken.None);

    private static Task<Result<Guid, StockError>> ApproveAsync(DisposalFixture store, Guid id, string role) =>
        new ApproveDisposalSlipCommandHandler(store.Store, store.Store, store.Employees, NullLogger<ApproveDisposalSlipCommandHandler>.Instance)
            .HandleAsync(new ApproveDocumentCommand(id, new EmployeeActor(Guid.NewGuid(), role, role is EmployeeRole.Admin or EmployeeRole.Director or EmployeeRole.Accountant ? null : store.Location.Id)), CancellationToken.None);

    private static DisposalFixture Prepared(int onHand, int reserved)
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var itemId = Guid.CreateVersion7();
        var balance = StockBalance.Create(itemId, location.Id, onHand, reserved, 0);
        store.Locations.Add(location);
        store.Balances.Add(balance);
        var employees = new FakeEmployeeRepository();
        var keeper = Employee.Create("Thủ Kho", "KhoA", "KHOA", "hash", EmployeeRole.WarehouseKeeper, stockLocationId: location.Id);
        employees.Employees.Add(keeper);
        return new DisposalFixture(store, location, itemId, balance, employees, keeper);
    }

    private sealed class DisposalFixture
    {
        private readonly FakeInventoryStore _store;

        public DisposalFixture(FakeInventoryStore store, StockLocation location, Guid itemId, StockBalance balance, FakeEmployeeRepository employees, Employee keeper)
        {
            _store = store;
            Location = location;
            ItemId = itemId;
            Balance = balance;
            Employees = employees;
            Keeper = keeper;
        }

        public StockLocation Location { get; }

        public Guid ItemId { get; }

        public StockBalance Balance { get; }

        public FakeEmployeeRepository Employees { get; }

        public Employee Keeper { get; }

        public FakeInventoryStore Store => _store;

        public List<DisposalSlip> Disposals => _store.Disposals;

        public List<Notification> Notices => _store.Notices;
    }
}
