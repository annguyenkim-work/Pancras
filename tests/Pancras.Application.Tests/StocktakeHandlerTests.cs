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

        Assert.Equal(StockError.MissingEvidence, missingReason.StockError);
        Assert.Equal(StockError.MissingEvidence, missingDocument.StockError);
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

        var result = await handler.HandleAsync(new ApproveDocumentCommand(created.Value, EmployeeRole.Sales), CancellationToken.None);

        Assert.Equal(StockError.ApprovalForbidden, result.StockError);
        Assert.Equal(ApprovalStatus.Pending, store.Stocktakes[0].Status);
    }

    [Fact]
    public async Task Approve_ShowroomLead_ClearsPending()
    {
        var store = Store(book: 10);
        var created = await CreateHandler(store).HandleAsync(Command(store, 9, "Lệch", "BB-01"), CancellationToken.None);
        var handler = new ApproveStocktakeCommandHandler(store.Store, NullLogger<ApproveStocktakeCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ApproveDocumentCommand(created.Value, EmployeeRole.ShowroomWarehouseLead), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, store.Stocktakes[0].Status);
        Assert.Equal(10, store.Balance.OnHand);
    }

    private static CreateStocktakeCommandHandler CreateHandler(StocktakeFixture store) =>
        new(store.Store, NullLogger<CreateStocktakeCommandHandler>.Instance);

    private static CreateStocktakeCommand Command(StocktakeFixture store, int counted, string? reason, string? document) =>
        new()
        {
            Role = store.Role,
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
