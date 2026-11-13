using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class InventoryScopeTests
{
    private readonly FakeInventoryStore _store = new();
    private readonly StockLocation _a = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
    private readonly StockLocation _b = StockLocation.Create("Showroom B", StockLocationKind.Showroom);
    private readonly Guid _itemId = Guid.CreateVersion7();
    private readonly StockBalance _balanceA;
    private readonly StockBalance _balanceB;

    public InventoryScopeTests()
    {
        _store.Locations.Add(_a);
        _store.Locations.Add(_b);
        _store.Items.Add(new StockItemInfo(_itemId, "SP01", "Vòi", "Toto", ProductCategory.SanitaryEquipment, 500));
        _balanceA = StockBalance.Create(_itemId, _a.Id, 10, 0, 0);
        _balanceB = StockBalance.Create(_itemId, _b.Id, 7, 0, 0);
        _store.Balances.Add(_balanceA);
        _store.Balances.Add(_balanceB);
    }

    private ListStockBalancesHandler List() => new(_store, NullLogger<ListStockBalancesHandler>.Instance);

    private static StockListFilter All() => StockListFilter.From(null, null, null);

    [Fact]
    public async Task KeeperSeesOnlyOwnLocation()
    {
        var rows = await List().HandleAsync(All(), TestActors.Keeper(_a.Id), CancellationToken.None);

        Assert.Equal(_a.Id, Assert.Single(rows).StockLocationId);
    }

    [Fact]
    public async Task KeeperAskingForAnotherLocation_GetsNothing()
    {
        var rows = await List().HandleAsync(StockListFilter.From(_b.Id.ToString("D"), null, null), TestActors.Keeper(_a.Id), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task EmployeeWithoutLocation_SeesNothing()
    {
        var rows = await List().HandleAsync(All(), TestActors.Keeper(Guid.Empty), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task FinanceSeesEveryLocation_AndCustomerCareSeesNone()
    {
        var finance = await List().HandleAsync(All(), TestActors.Accountant, CancellationToken.None);
        var care = await List().HandleAsync(All(), new EmployeeActor(Guid.NewGuid(), EmployeeRole.CustomerCare, null), CancellationToken.None);

        Assert.Equal(2, finance.Count);
        Assert.Empty(care);
    }

    [Fact]
    public async Task CostIsHiddenFromEveryoneButFinance()
    {
        var sales = await List().HandleAsync(All(), TestActors.Sales(_a.Id), CancellationToken.None);
        var lead = await List().HandleAsync(All(), TestActors.Lead(_a.Id), CancellationToken.None);
        var admin = await List().HandleAsync(All(), TestActors.Admin, CancellationToken.None);

        Assert.All(sales, row => Assert.Equal(0, row.CostPrice));
        Assert.All(lead, row => Assert.Equal(0, row.CostPrice));
        Assert.All(admin, row => Assert.Equal(500, row.CostPrice));
    }

    [Fact]
    public async Task ExportIsForFinanceAndLeadsOnly()
    {
        var keeper = await List().ExportAsync(All(), TestActors.Keeper(_a.Id), CancellationToken.None);
        var sales = await List().ExportAsync(All(), TestActors.Sales(_a.Id), CancellationToken.None);
        var lead = await List().ExportAsync(All(), TestActors.Lead(_a.Id), CancellationToken.None);

        Assert.Empty(keeper);
        Assert.Empty(sales);
        Assert.Equal(_a.Id, Assert.Single(lead).StockLocationId);
    }

    [Fact]
    public async Task KeeperCanRequestForOwnLocation_ButShipOnlyFromIt_AndSalesCannotRequest()
    {
        var own = await TransferFlow.RequestAsync(_store, Transfer(TestActors.Keeper(_a.Id), _a.Id, _b.Id));
        var asked = await TransferFlow.RequestAsync(_store, Transfer(TestActors.Keeper(_a.Id), _b.Id, _a.Id));
        var sales = await TransferFlow.RequestAsync(_store, Transfer(TestActors.Sales(_a.Id), _a.Id, _b.Id));
        var unrelated = await TransferFlow.RequestAsync(_store, Transfer(TestActors.Keeper(Guid.NewGuid()), _a.Id, _b.Id));
        await new DecideTransferCommandHandler(_store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(asked.Value, TestActors.Admin, true), CancellationToken.None);
        var ship = new ShipTransferCommandHandler(_store, NullLogger<ShipTransferCommandHandler>.Instance);

        var notSource = await ship.HandleAsync(new ShipTransferCommand(asked.Value, TestActors.Keeper(_a.Id)), CancellationToken.None);

        Assert.True(own.IsSuccess);
        Assert.True(asked.IsSuccess);
        Assert.Equal(StockError.Forbidden, sales.StockError);
        Assert.Equal(StockError.Forbidden, unrelated.StockError);
        Assert.Equal(StockError.Forbidden, notSource.StockError);
    }

    [Fact]
    public async Task OnlyTheDestinationConfirmsReceipt()
    {
        var shipped = await TransferFlow.ShipAsync(_store, Transfer(TestActors.Keeper(_a.Id), _a.Id, _b.Id, 2));
        var receipt = new ConfirmTransferReceiptCommandHandler(_store, NullLogger<ConfirmTransferReceiptCommandHandler>.Instance);

        var bySource = await receipt.HandleAsync(new ConfirmTransferCommand(shipped.Value, TestActors.Keeper(_a.Id)), CancellationToken.None);
        var byDestination = await receipt.HandleAsync(new ConfirmTransferCommand(shipped.Value, TestActors.Keeper(_b.Id)), CancellationToken.None);

        Assert.True(shipped.IsSuccess);
        Assert.Equal(StockError.Forbidden, bySource.StockError);
        Assert.True(byDestination.IsSuccess);
    }

    [Fact]
    public async Task DocumentListsAreScopedAndClosedToSales()
    {
        await TransferFlow.RequestAsync(_store, Transfer(TestActors.Admin, _a.Id, _b.Id));
        var documents = new ListInventoryDocumentsHandler(_store, NullLogger<ListInventoryDocumentsHandler>.Instance);
        var third = StockLocation.Create("Kho C", StockLocationKind.Warehouse);
        _store.Locations.Add(third);

        var source = await documents.ListTransfersAsync(TestActors.Keeper(_a.Id), CancellationToken.None);
        var destination = await documents.ListTransfersAsync(TestActors.Keeper(_b.Id), CancellationToken.None);
        var unrelated = await documents.ListTransfersAsync(TestActors.Keeper(third.Id), CancellationToken.None);
        var sales = await documents.ListTransfersAsync(TestActors.Sales(_a.Id), CancellationToken.None);

        Assert.Single(source);
        Assert.Single(destination);
        Assert.Empty(unrelated);
        Assert.Empty(sales);
    }

    [Fact]
    public async Task AccountantDisposal_IsPostedImmediately_ButKeeperDisposalWaits()
    {
        var create = DisposalCreator(out _);

        var direct = await create.HandleAsync(Disposal(TestActors.Accountant), CancellationToken.None);
        var drafted = await create.HandleAsync(Disposal(TestActors.Keeper(_a.Id)), CancellationToken.None);

        Assert.True(direct.IsSuccess);
        Assert.True(drafted.IsSuccess);
        Assert.Equal(9, _balanceA.OnHand);
        Assert.Equal(ApprovalStatus.Approved, _store.Disposals[0].Status);
        Assert.Equal(ApprovalStatus.Pending, _store.Disposals[1].Status);
    }

    [Fact]
    public async Task AccountantDisposal_WithNoStock_IsRejected_AndLeavesNoSlipBehind()
    {
        var create = DisposalCreator(out _);
        var empty = StockBalance.Create(_itemId, _b.Id, 0, 0, 0);
        _store.Balances.Remove(_balanceB);
        _store.Balances.Add(empty);
        var command = Disposal(TestActors.Accountant);
        command.StockLocationId = _b.Id;

        var rejected = await create.HandleAsync(command, CancellationToken.None);

        Assert.True(rejected.IsFailure);
        Assert.Empty(_store.Disposals);
        Assert.Empty(await _store.ListDisposalsAsync(CancellationToken.None));
        Assert.Equal(0, empty.OnHand);
    }

    [Fact]
    public async Task LeadApprovesAKeeperSlip_ButNotTheirOwn_AndNotInAnotherLocation()
    {
        var create = DisposalCreator(out var approve);
        var byKeeper = await create.HandleAsync(Disposal(TestActors.Keeper(_a.Id)), CancellationToken.None);
        var byLead = await create.HandleAsync(Disposal(TestActors.Lead(_a.Id)), CancellationToken.None);

        var otherLead = await approve.HandleAsync(new ApproveDocumentCommand(byKeeper.Value, TestActors.Lead(_b.Id)), CancellationToken.None);
        var ownSlip = await approve.HandleAsync(new ApproveDocumentCommand(byLead.Value, TestActors.Lead(_a.Id)), CancellationToken.None);
        var ok = await approve.HandleAsync(new ApproveDocumentCommand(byKeeper.Value, TestActors.Lead(_a.Id)), CancellationToken.None);
        var byDirector = await approve.HandleAsync(new ApproveDocumentCommand(byLead.Value, TestActors.Director), CancellationToken.None);

        Assert.Equal(StockError.ApprovalForbidden, otherLead.StockError);
        Assert.Equal(StockError.ApprovalForbidden, ownSlip.StockError);
        Assert.True(ok.IsSuccess);
        Assert.True(byDirector.IsSuccess);
    }

    [Fact]
    public async Task FinanceStocktake_IsRecordedAsApproved_AndKeeperStocktakeWaits()
    {
        var create = new CreateStocktakeCommandHandler(_store, NullLogger<CreateStocktakeCommandHandler>.Instance);
        CreateStocktakeCommand Command(EmployeeActor actor) => new()
        {
            Actor = actor,
            StockLocationId = _a.Id,
            CatalogItemId = _itemId,
            CountedQuantity = 9,
            Reason = "Lệch",
            DocumentRef = "BB-01"
        };

        await create.HandleAsync(Command(TestActors.Director), CancellationToken.None);
        await create.HandleAsync(Command(TestActors.Keeper(_a.Id)), CancellationToken.None);
        var salesTry = await create.HandleAsync(Command(TestActors.Sales(_a.Id)), CancellationToken.None);

        Assert.Equal(ApprovalStatus.Approved, _store.Stocktakes[0].Status);
        Assert.Equal(ApprovalStatus.Pending, _store.Stocktakes[1].Status);
        Assert.Equal(StockError.Forbidden, salesTry.StockError);
    }

    private CreateTransferSlipCommand Transfer(EmployeeActor actor, Guid source, Guid destination, int quantity = 1) =>
        new()
        {
            SourceLocationId = source,
            DestinationLocationId = destination,
            Actor = actor,
            Lines = [new TransferLineInput(_itemId, quantity)]
        };

    private CreateDisposalSlipCommand Disposal(EmployeeActor actor) =>
        new()
        {
            Actor = actor,
            StockLocationId = _a.Id,
            CatalogItemId = _itemId,
            Reason = "Vỡ",
            DocumentRef = "HH-01"
        };

    private CreateDisposalSlipCommandHandler DisposalCreator(out ApproveDisposalSlipCommandHandler approve)
    {
        approve = new ApproveDisposalSlipCommandHandler(_store, new FakeEmployeeRepository(), NullLogger<ApproveDisposalSlipCommandHandler>.Instance);
        return new CreateDisposalSlipCommandHandler(_store, approve, NullLogger<CreateDisposalSlipCommandHandler>.Instance);
    }
}
