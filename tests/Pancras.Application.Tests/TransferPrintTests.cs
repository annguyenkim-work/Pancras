using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

/// <summary>Quyền và trạng thái in phiếu chuyển kho: tài chính in mọi phiếu; Thủ kho, Trưởng kho chỉ in phiếu của kho xuất.</summary>
public class TransferPrintTests
{
    private readonly FakeInventoryStore _store = new();
    private readonly StockLocation _source = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
    private readonly StockLocation _destination = StockLocation.Create("Showroom B", StockLocationKind.Showroom);
    private readonly Guid _itemId = Guid.CreateVersion7();

    public TransferPrintTests()
    {
        _store.Locations.Add(_source);
        _store.Locations.Add(_destination);
        _store.Items.Add(new StockItemInfo(_itemId, "SP-01", "Bồn cầu", "TOTO", Pancras.Domain.Catalog.ProductCategory.SanitaryEquipment));
        _store.Balances.Add(StockBalance.Create(_itemId, _source.Id, 100, 0, 0));
        _store.Balances.Add(StockBalance.Create(_itemId, _destination.Id, 0, 0, 0));
    }

    [Fact]
    public async Task Requested_CannotBePrinted_ByAnyone()
    {
        var id = await RequestAsync();

        var result = await PrintAsync(id, TestActors.Admin);

        Assert.Equal(StockError.TransferNotPrintable, result.Error);
    }

    [Fact]
    public async Task Rejected_And_Cancelled_CannotBePrinted()
    {
        var rejected = await RequestAsync();
        await new DecideTransferCommandHandler(_store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(rejected, TestActors.Admin, false, "Không đủ"), CancellationToken.None);
        var cancelled = await RequestAsync();
        await new CancelTransferCommandHandler(_store, NullLogger<CancelTransferCommandHandler>.Instance)
            .HandleAsync(new ConfirmTransferCommand(cancelled, TestActors.Admin, null, "Hủy"), CancellationToken.None);

        Assert.Equal(StockError.TransferNotPrintable, (await PrintAsync(rejected, TestActors.Admin)).Error);
        Assert.Equal(StockError.TransferNotPrintable, (await PrintAsync(cancelled, TestActors.Admin)).Error);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("director")]
    [InlineData("accountant")]
    public async Task Finance_PrintsApprovedAndInTransitSlips_ForAnyLocation(string who)
    {
        var approved = await ApproveAsync();
        var inTransit = await ShipAsync();
        var actor = who switch
        {
            "admin" => TestActors.Admin,
            "director" => TestActors.Director,
            _ => TestActors.Accountant
        };

        var first = await PrintAsync(approved, actor);
        var second = await PrintAsync(inTransit, actor);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(approved, first.Value!.Id);
        Assert.Equal("Kho A", first.Value!.SourceName);
        Assert.Equal("Showroom B", first.Value!.DestinationName);
        Assert.Equal("SP-01", Assert.Single(first.Value!.Lines).Code);
    }

    [Fact]
    public async Task KeeperAndLead_OfTheSourceLocation_CanPrint()
    {
        var id = await ApproveAsync();

        Assert.True((await PrintAsync(id, TestActors.Keeper(_source.Id))).IsSuccess);
        Assert.True((await PrintAsync(id, TestActors.Lead(_source.Id))).IsSuccess);
    }

    [Fact]
    public async Task KeeperAndLead_OfTheDestinationLocation_CanViewButNotPrint()
    {
        var id = await ApproveAsync();
        var keeper = TestActors.Keeper(_destination.Id);
        var rows = await new ListInventoryDocumentsHandler(_store, NullLogger<ListInventoryDocumentsHandler>.Instance)
            .ListTransfersAsync(keeper, CancellationToken.None);

        Assert.Contains(rows, row => row.Id == id);
        Assert.False(rows.Single(row => row.Id == id).Flow!.CanPrint);
        Assert.Equal(StockError.Forbidden, (await PrintAsync(id, keeper)).Error);
        Assert.Equal(StockError.Forbidden, (await PrintAsync(id, TestActors.Lead(_destination.Id))).Error);
    }

    [Fact]
    public async Task UnrelatedKeeper_And_Sales_SeeNoSuchSlip()
    {
        var id = await ApproveAsync();
        var other = StockLocation.Create("Kho C", StockLocationKind.Warehouse);
        _store.Locations.Add(other);

        Assert.Equal(StockError.TransferNotFound, (await PrintAsync(id, TestActors.Keeper(other.Id))).Error);
        Assert.Equal(StockError.TransferNotFound, (await PrintAsync(id, TestActors.Sales(_source.Id))).Error);
        Assert.Equal(StockError.TransferNotFound, (await PrintAsync(Guid.NewGuid(), TestActors.Admin)).Error);
    }

    [Fact]
    public async Task TheListRows_FlagCanPrint_OnlyWherePrintingIsAllowed()
    {
        var requested = await RequestAsync();
        var approved = await ApproveAsync();
        var rows = await new ListInventoryDocumentsHandler(_store, NullLogger<ListInventoryDocumentsHandler>.Instance)
            .ListTransfersAsync(TestActors.Admin, CancellationToken.None);

        Assert.False(rows.Single(row => row.Id == requested).Flow!.CanPrint);
        Assert.True(rows.Single(row => row.Id == approved).Flow!.CanPrint);
    }

    private async Task<Result<TransferSlipRow, StockError>> PrintAsync(Guid id, EmployeeActor actor) =>
        await new GetTransferPrintHandler(new ListInventoryDocumentsHandler(_store, NullLogger<ListInventoryDocumentsHandler>.Instance))
            .HandleAsync(new GetTransferPrintQuery(id, actor), CancellationToken.None);

    private async Task<Guid> RequestAsync() =>
        (await TransferFlow.RequestAsync(_store, new CreateTransferSlipCommand
        {
            SourceLocationId = _source.Id,
            DestinationLocationId = _destination.Id,
            Note = "Xe sáng",
            Actor = TestActors.Admin,
            Lines = [new TransferLineInput(_itemId, 2)]
        })).Value;

    private async Task<Guid> ApproveAsync()
    {
        var id = await RequestAsync();
        await new DecideTransferCommandHandler(_store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(id, TestActors.Admin, true), CancellationToken.None);
        return id;
    }

    private async Task<Guid> ShipAsync()
    {
        var id = await ApproveAsync();
        await new ShipTransferCommandHandler(_store, NullLogger<ShipTransferCommandHandler>.Instance)
            .HandleAsync(new ShipTransferCommand(id, TestActors.Admin, "SEAL-1", "Anh Bình"), CancellationToken.None);
        return id;
    }
}
