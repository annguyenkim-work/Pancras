using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class StocktakeHandlerTests
{
    [Fact]
    public async Task Create_RejectsAVarianceWithoutReasonOrDocument()
    {
        var store = Store(book: 10);
        var handler = CreateHandler(store);

        var missingReason = await handler.HandleAsync(Command(store, 8, reason: " ", document: "BB-01"), CancellationToken.None);
        var missingDocument = await handler.HandleAsync(Command(store, 8, reason: "Đếm lại", document: null), CancellationToken.None);

        Assert.Equal(StockError.MissingEvidence, missingReason.Error);
        Assert.Equal(StockError.MissingEvidence, missingDocument.Error);
        Assert.Empty(store.Stocktakes);
        Assert.Equal(10, store.Balance.OnHand);
    }

    [Fact]
    public async Task Create_AllowsAnEmptyReasonWhenTheCountMatchesTheBook()
    {
        var store = Store(book: 10);
        var handler = CreateHandler(store);

        var result = await handler.HandleAsync(Command(store, 10, reason: null, document: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sheet = Assert.Single(store.Stocktakes);
        Assert.Equal(string.Empty, sheet.Reason);
        Assert.Equal(string.Empty, sheet.DocumentRef);
        Assert.Equal(ApprovalStatus.Pending, sheet.Status);
    }

    [Fact]
    public async Task WarehouseKeeper_CanCreateAStocktake()
    {
        var store = Store(book: 10);
        var result = await CreateHandler(store).HandleAsync(Command(store, 9, "Lệch", "BB-01"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeRole.WarehouseKeeper, store.Role);
        Assert.Equal(ApprovalStatus.Pending, Assert.Single(store.Stocktakes).Status);
    }

    [Fact]
    public async Task Approve_RejectsARoleOtherThanTheShowroomLead_AndKeepsPending()
    {
        var store = Store(book: 10);
        var created = await CreateHandler(store).HandleAsync(Command(store, 9, "Lệch", "BB-01"), CancellationToken.None);
        var handler = new ApproveStocktakeCommandHandler(store.Store, NullLogger<ApproveStocktakeCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Sales(store.Location.Id)), CancellationToken.None);

        Assert.Equal(StockError.ApprovalForbidden, result.Error);
        Assert.Equal(ApprovalStatus.Pending, store.Stocktakes[0].Status);
    }

    [Fact]
    public async Task Approve_ShowroomLead_ClearsPending()
    {
        var store = Store(book: 10);
        var created = await CreateHandler(store).HandleAsync(Command(store, 9, "Lệch", "BB-01"), CancellationToken.None);
        var handler = new ApproveStocktakeCommandHandler(store.Store, NullLogger<ApproveStocktakeCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Lead(store.Location.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, store.Stocktakes[0].Status);
        Assert.Equal(9, store.Balance.OnHand);
    }

    [Fact]
    public async Task Approve_SetsOnHandToTheCountedQuantity_AndRecordsAMovement()
    {
        var store = Store(book: 5);
        var created = await CreateHandler(store).HandleAsync(Command(store, 3, "Lệch", "BB-01"), CancellationToken.None);
        var handler = new ApproveStocktakeCommandHandler(store.Store, NullLogger<ApproveStocktakeCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Lead(store.Location.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, store.Balance.OnHand);
        var movement = Assert.Single(store.Store.Movements);
        Assert.Equal(-2, movement.Quantity);
    }

    [Fact]
    public async Task Approve_AllowsACountBelowTheReservedQuantity()
    {
        var store = Store(book: 10);
        store.Balance.TryReserve(6);
        var created = await CreateHandler(store).HandleAsync(Command(store, 4, "Lệch", "BB-01"), CancellationToken.None);
        var handler = new ApproveStocktakeCommandHandler(store.Store, NullLogger<ApproveStocktakeCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Lead(store.Location.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, store.Balance.OnHand);
        Assert.Equal(6, store.Balance.Reserved);
    }

    [Fact]
    public async Task Approve_AppliesTheVarianceToTheCurrentStock_WhenTheBookMovedMeanwhile()
    {
        var store = Store(book: 26);
        var created = await CreateHandler(store).HandleAsync(Command(store, 24, "Lệch", "BB-01"), CancellationToken.None);
        store.Balance.TryAdjust(5);
        var handler = new ApproveStocktakeCommandHandler(store.Store, NullLogger<ApproveStocktakeCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Lead(store.Location.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(29, store.Balance.OnHand);
    }

    [Fact]
    public async Task Create_RejectsANegativeCount()
    {
        var store = Store(book: 5);

        var result = await CreateHandler(store).HandleAsync(Command(store, -1, "Lệch", "BB-01"), CancellationToken.None);

        Assert.Equal(StockError.InvalidCount, result.Error);
        Assert.Empty(store.Stocktakes);
    }

    [Fact]
    public async Task Create_ByAccountant_AppliesTheCountImmediately()
    {
        var store = Store(book: 5);
        var command = Command(store, 3, "Lệch", "BB-01");
        command.Actor = TestActors.Accountant with { LocationId = store.Location.Id };

        var result = await CreateHandler(store).HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, store.Stocktakes[0].Status);
        Assert.Equal(3, store.Balance.OnHand);
    }

    [Fact]
    public async Task Create_ByStaff_NotifiesTheLocationLeadOnly_AndApprovalClearsTheNotice()
    {
        var store = Store(book: 10);
        var employees = new FakeEmployeeRepository();
        var lead = Employee.Create("Trưởng kho", "lead", "LEAD", "hash", EmployeeRole.ShowroomWarehouseLead, stockLocationId: store.Location.Id);
        var otherLead = Employee.Create("Trưởng khác", "lead2", "LEAD2", "hash", EmployeeRole.ShowroomWarehouseLead, stockLocationId: Guid.NewGuid());
        var director = Employee.Create("Giám đốc", "gd", "GD", "hash", EmployeeRole.Director);
        employees.Employees.AddRange([lead, otherLead, director]);
        var create = new CreateStocktakeCommandHandler(store.Store, NullLogger<CreateStocktakeCommandHandler>.Instance, employees: employees);

        var created = await create.HandleAsync(Command(store, 8, "Lệch", "BB-01"), CancellationToken.None);

        var notice = Assert.Single(store.Store.Notices);
        Assert.Equal(lead.Id, notice.RecipientEmployeeId);
        Assert.Equal(Pancras.Domain.Notifications.NotificationKind.StocktakePending, notice.Kind);
        Assert.Equal(created.Value, notice.DocumentId);

        var approve = new ApproveStocktakeCommandHandler(store.Store, NullLogger<ApproveStocktakeCommandHandler>.Instance);
        var approved = await approve.HandleAsync(new ApproveDocumentCommand(created.Value, TestActors.Lead(store.Location.Id)), CancellationToken.None);

        Assert.True(approved.IsSuccess);
        Assert.Empty(store.Store.Notices);
    }

    private static CreateStocktakeCommandHandler CreateHandler(StocktakeFixture store) =>
        new(store.Store, NullLogger<CreateStocktakeCommandHandler>.Instance);

    private static CreateStocktakeCommand Command(StocktakeFixture store, int counted, string? reason, string? document) =>
        new()
        {
            Actor = new EmployeeActor(Guid.NewGuid(), store.Role, store.Location.Id),
            StockLocationId = store.Location.Id,
            CatalogItemId = store.ItemId,
            CountedQuantity = counted,
            Reason = reason,
            DocumentRef = document
        };

    private static StocktakeFixture Store(int book)
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var itemId = Guid.CreateVersion7();
        var balance = StockBalance.Create(itemId, location.Id, book, 0, 0);
        store.Locations.Add(location);
        store.Balances.Add(balance);
        return new StocktakeFixture(store, location, itemId, balance, EmployeeRole.WarehouseKeeper);
    }

    private sealed class StocktakeFixture
    {
        private readonly FakeInventoryStore _store;

        public StocktakeFixture(FakeInventoryStore store, StockLocation location, Guid itemId, StockBalance balance, string role)
        {
            _store = store;
            Location = location;
            ItemId = itemId;
            Balance = balance;
            Role = role;
        }

        public StockLocation Location { get; }

        public Guid ItemId { get; }

        public StockBalance Balance { get; }

        public string Role { get; }

        public FakeInventoryStore Store => _store;

        public List<StocktakeSheet> Stocktakes => _store.Stocktakes;
    }
}
