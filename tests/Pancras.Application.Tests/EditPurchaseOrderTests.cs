using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class EditPurchaseOrderTests
{
    private readonly FakePurchaseStore _orders = new();
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeInventoryStore _inventory = new();
    private readonly FakeCatalogRepository _catalog = new();
    private readonly Supplier _supplier = Supplier.Create("NCC1", "Nhà A", "Việt Nam", "An", "0900000000", "a@b.co", "TK");
    private readonly StockLocation _location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
    private readonly CatalogItem _first = Item("SP01");
    private readonly CatalogItem _second = Item("SP02");
    private readonly PurchaseOrder _order;

    public EditPurchaseOrderTests()
    {
        _suppliers.Suppliers.Add(_supplier);
        _inventory.Locations.Add(_location);
        _catalog.Items.Add(_first);
        _catalog.Items.Add(_second);
        _order = PurchaseOrder.TryCreate(
            "PO-1",
            _supplier.Id,
            _location.Id,
            "CONT",
            null,
            null,
            null,
            25000m,
            0.10m,
            [(_first.Id, 2, 10m, 0.10m, 0m, 0m, 0m), (_second.Id, 3, 30m, 0.10m, 0m, 0m, 0m)])!;
        _orders.Orders.Add(_order);
    }

    private static CatalogItem Item(string code) =>
        CatalogItem.Create(code, code, ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 1m, null, null, null, 1m, 1m, 1m, 1m, 1m);

    private UpdatePurchaseOrderCommandHandler Update() =>
        new(
            _orders,
            _suppliers,
            _inventory,
            _catalog,
            new GetCatalogMeasuresHandler(_catalog, NullLogger<GetCatalogMeasuresHandler>.Instance),
            NullLogger<UpdatePurchaseOrderCommandHandler>.Instance);

    private UpdatePurchaseOrderCommand Command(params CreatePurchaseLineInput[] lines) =>
        new()
        {
            PurchaseOrderId = _order.Id,
            Code = "PO-1",
            SupplierId = _supplier.Id,
            ReceiveLocationId = _location.Id,
            ExchangeRate = 25000m,
            VatRate = 0.10m,
            Lines = lines
        };

    private CreatePurchaseLineInput Keep(int index, int quantity, decimal fob) =>
        new() { LineId = _order.Lines[index].Id, CatalogItemId = _order.Lines[index].CatalogItemId, Quantity = quantity, FobUsd = fob, DutyRate = 0.10m };

    private Guid AddFobCharge()
    {
        Assert.True(_order.TryAddCharge(40m, ChargeSplitMethod.FobValue, out var id, "THC"));
        Assert.True(_order.TryAllocate(id, new Dictionary<Guid, decimal> { [_order.Lines[0].Id] = 10m, [_order.Lines[1].Id] = 30m }, false));
        Assert.True(_order.TryAssignGroup(id, ChargeGroup.Logistics, false));
        return id;
    }

    [Fact]
    public async Task EditingPrices_SplitsTheChargesAgainOverTheSameLines()
    {
        AddFobCharge();
        var before = _order.Lines.Select(line => line.Id).ToArray();

        var result = await Update().HandleAsync(Command(Keep(0, 2, 30m), Keep(1, 3, 30m)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(before, _order.Lines.Select(line => line.Id).ToArray());
        Assert.Equal(30m, _order.Lines[0].FobUsd);
        Assert.Equal([20m, 20m], _order.Charges[0].Shares.Select(share => share.Amount).ToArray());
    }

    [Fact]
    public async Task AddingAndRemovingLines_KeepsTheChargeTotalWhole()
    {
        AddFobCharge();

        var result = await Update().HandleAsync(
            Command(
                Keep(0, 2, 10m),
                new CreatePurchaseLineInput { CatalogItemId = _second.Id, Quantity = 1, FobUsd = 10m, DutyRate = 0m }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _order.Lines.Count);
        Assert.Equal(40m, _order.Charges[0].Shares.Sum(share => share.Amount));
        Assert.Equal(2, _order.Charges[0].Shares.Count);
    }

    [Fact]
    public async Task AChargeSplitByHand_BlocksChangingTheLineSet_ButNotThePrices()
    {
        Assert.True(_order.TryAddCharge(40m, ChargeSplitMethod.Manual, out var id, "Tay"));
        Assert.True(_order.TryAllocate(id, new Dictionary<Guid, decimal> { [_order.Lines[0].Id] = 15m, [_order.Lines[1].Id] = 25m }, true));
        Assert.True(_order.TryAssignGroup(id, ChargeGroup.Logistics, false));

        var added = await Update().HandleAsync(
            Command(Keep(0, 2, 10m), Keep(1, 3, 30m), new CreatePurchaseLineInput { CatalogItemId = _first.Id, Quantity = 1, FobUsd = 1m }),
            CancellationToken.None);
        var repriced = await Update().HandleAsync(Command(Keep(0, 5, 10m), Keep(1, 3, 30m)), CancellationToken.None);

        Assert.Equal(PurchaseError.ManualChargeLocked, added.PurchaseError);
        Assert.Equal(2, _order.Lines.Count);
        Assert.True(repriced.IsSuccess);
        Assert.Equal([15m, 25m], _order.Charges[0].Shares.Select(share => share.Amount).ToArray());
    }

    private UpdatePurchaseOrderCommand WithCharges(params UpdatePurchaseChargeInput[] charges)
    {
        var command = Command(Keep(0, 2, 10m), Keep(1, 3, 30m));
        command.Charges = charges;
        return command;
    }

    [Fact]
    public async Task EditingAnOrder_StampsTheLastModifiedTimeAndPerson()
    {
        var at = new DateTimeOffset(2026, 11, 14, 8, 30, 0, TimeSpan.Zero);
        var handler = new UpdatePurchaseOrderCommandHandler(
            _orders,
            _suppliers,
            _inventory,
            _catalog,
            new GetCatalogMeasuresHandler(_catalog, NullLogger<GetCatalogMeasuresHandler>.Instance),
            NullLogger<UpdatePurchaseOrderCommandHandler>.Instance,
            new TestClock(at),
            null,
            new FakeRequestInfo("KeToan1"));
        var createdAt = _order.CreatedAt;

        var result = await handler.HandleAsync(Command(Keep(0, 3, 10m), Keep(1, 3, 30m)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(at, _order.UpdatedAt);
        Assert.Equal("KeToan1", _order.UpdatedBy);
        Assert.Equal(createdAt, _order.CreatedAt);
    }

    [Fact]
    public async Task EditingAChargeRow_ChangesItsAmountLabelGroupAndSplit()
    {
        var id = AddFobCharge();

        var result = await Update().HandleAsync(
            WithCharges(new UpdatePurchaseChargeInput { ChargeId = id, Amount = 100m, Label = "Cước mới", Group = ChargeGroup.CustomsValueAdjustment, Method = ChargeSplitMethod.Quantity }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var charge = Assert.Single(_order.Charges);
        Assert.Equal(100m, charge.Amount);
        Assert.Equal("Cước mới", charge.Label);
        Assert.Equal(ChargeGroup.CustomsValueAdjustment, charge.Group);
        Assert.Equal(ChargeSplitMethod.Quantity, charge.Method);
        Assert.Equal([40m, 60m], charge.Shares.Select(share => share.Amount).ToArray());
    }

    [Fact]
    public async Task ANewChargeRow_IsAddedSplitAndGrouped_AndAMissingRowIsDeleted()
    {
        var old = AddFobCharge();

        var result = await Update().HandleAsync(
            WithCharges(new UpdatePurchaseChargeInput { Amount = 500m, Label = "Kéo xe", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.FobValue }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var charge = Assert.Single(_order.Charges);
        Assert.NotEqual(old, charge.Id);
        Assert.Equal("Kéo xe", charge.Label);
        Assert.Equal(ChargeGroup.Logistics, charge.Group);
        Assert.Equal(500m, charge.Shares.Sum(share => share.Amount));
    }

    [Fact]
    public async Task AChargeRowWithZeroAmount_IsDropped_AndNullChargesLeaveEverythingAlone()
    {
        var id = AddFobCharge();

        var untouched = await Update().HandleAsync(Command(Keep(0, 2, 10m), Keep(1, 3, 30m)), CancellationToken.None);
        Assert.True(untouched.IsSuccess);
        Assert.Single(_order.Charges);

        var dropped = await Update().HandleAsync(
            WithCharges(new UpdatePurchaseChargeInput { ChargeId = id, Amount = 0m, Label = "THC", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.FobValue }),
            CancellationToken.None);

        Assert.True(dropped.IsSuccess);
        Assert.Empty(_order.Charges);
    }

    [Fact]
    public async Task AVolumeSplitWithoutMeasures_IsRefused_NotSilentlyChanged()
    {
        var id = AddFobCharge();
        _order.TryUpdateCharge(id, 40m, ChargeSplitMethod.FobValue, "THC");

        var result = await Update().HandleAsync(
            WithCharges(new UpdatePurchaseChargeInput { ChargeId = id, Amount = 40m, Label = "THC", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.Volume }),
            CancellationToken.None);

        Assert.Equal(PurchaseError.MissingMeasure, result.PurchaseError);
    }

    [Fact]
    public async Task AManualChargeIsKeptAsIs_OrDeletedWhichUnlocksChangingTheLines()
    {
        Assert.True(_order.TryAddCharge(40m, ChargeSplitMethod.Manual, out var manual, "Tay"));
        Assert.True(_order.TryAllocate(manual, new Dictionary<Guid, decimal> { [_order.Lines[0].Id] = 15m, [_order.Lines[1].Id] = 25m }, true));
        Assert.True(_order.TryAssignGroup(manual, ChargeGroup.Logistics, false));
        var keepManual = new UpdatePurchaseChargeInput { ChargeId = manual, Amount = 999m, Label = "x", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.Manual };
        var withNewLine = new CreatePurchaseLineInput { CatalogItemId = _first.Id, Quantity = 1, FobUsd = 5m, DutyRate = 0m };

        var kept = await Update().HandleAsync(WithCharges(keepManual), CancellationToken.None);
        Assert.True(kept.IsSuccess);
        Assert.Equal(40m, _order.Charges.Single().Amount);
        Assert.Equal([15m, 25m], _order.Charges.Single().Shares.Select(share => share.Amount).ToArray());

        var locked = Command(Keep(0, 2, 10m), Keep(1, 3, 30m), withNewLine);
        locked.Charges = [keepManual];
        var refused = await Update().HandleAsync(locked, CancellationToken.None);
        Assert.Equal(PurchaseError.ManualChargeLocked, refused.PurchaseError);

        var unlocked = Command(Keep(0, 2, 10m), Keep(1, 3, 30m), withNewLine);
        unlocked.Charges = [];
        var allowed = await Update().HandleAsync(unlocked, CancellationToken.None);
        Assert.True(allowed.IsSuccess);
        Assert.Empty(_order.Charges);
        Assert.Equal(3, _order.Lines.Count);
    }

    [Fact]
    public async Task ASelectedLineWithQuantityZero_IsRefused_InsteadOfSilentlyDeletingIt()
    {
        AddFobCharge();

        var result = await Update().HandleAsync(
            Command(
                Keep(0, 0, 10m),
                Keep(1, 3, 30m),
                new CreatePurchaseLineInput()),
            CancellationToken.None);

        Assert.Equal(PurchaseError.InvalidQuantity, result.PurchaseError);
        Assert.Equal(2, _order.Lines.Count);
        Assert.Equal(2, _order.Lines[0].Quantity);
        Assert.Equal(40m, _order.Charges[0].Shares.Sum(share => share.Amount));
    }

    [Fact]
    public async Task ABlankExtraRow_IsIgnored()
    {
        var result = await Update().HandleAsync(
            Command(Keep(0, 2, 10m), Keep(1, 3, 30m), new CreatePurchaseLineInput()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _order.Lines.Count);
    }

    [Fact]
    public async Task AReceivedOrder_CannotBeEdited_NorLoseCharges()
    {
        AddFobCharge();
        Assert.True(_order.TryReceive());

        var edit = await Update().HandleAsync(Command(Keep(0, 9, 10m), Keep(1, 3, 30m)), CancellationToken.None);
        var delete = await Update().HandleAsync(WithCharges(), CancellationToken.None);

        Assert.Equal(PurchaseError.AlreadyReceived, edit.PurchaseError);
        Assert.Equal(PurchaseError.AlreadyReceived, delete.PurchaseError);
        Assert.Equal(2, _order.Lines[0].Quantity);
        Assert.Single(_order.Charges);
    }

    [Fact]
    public async Task TheCodeMustStayUnique_ButKeepingYourOwnCodeIsFine()
    {
        var other = PurchaseOrder.TryCreate("PO-2", _supplier.Id, _location.Id, null, null, null, null, 25000m, 0m, [(_first.Id, 1, 1m, 0m, 0m, 0m, 0m)])!;
        _orders.Orders.Add(other);
        var clash = Command(Keep(0, 2, 10m), Keep(1, 3, 30m));
        clash.Code = "po-2";

        var rejected = await Update().HandleAsync(clash, CancellationToken.None);
        var same = await Update().HandleAsync(Command(Keep(0, 2, 10m), Keep(1, 3, 30m)), CancellationToken.None);

        Assert.Equal(PurchaseError.DuplicateCode, rejected.PurchaseError);
        Assert.True(same.IsSuccess);
        Assert.Equal("PO-1", _order.Code);
    }

    [Fact]
    public async Task RemovingTheChargeRowDeletesItAndItsCost()
    {
        AddFobCharge();
        var costBefore = PurchaseCosting.For(_order, _order.Lines[0]).LogisticsAllocated;

        var result = await Update().HandleAsync(WithCharges(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_order.Charges);
        Assert.Equal(10m, costBefore);
        Assert.Equal(0m, PurchaseCosting.For(_order, _order.Lines[0]).LogisticsAllocated);
    }
}
