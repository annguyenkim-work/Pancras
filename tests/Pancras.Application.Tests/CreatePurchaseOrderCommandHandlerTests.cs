using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class CreatePurchaseOrderCommandHandlerTests
{
    [Fact]
    public async Task ValidOrder_StoresTwoLinesAndOneVatRate()
    {
        var fixture = new PurchaseFixture();
        var first = Item("SP01", 2m);
        var second = Item("SP02", 6m);
        fixture.Catalog.Items.Add(first);
        fixture.Catalog.Items.Add(second);

        var result = await fixture.Create.HandleAsync(Command(fixture, first.Id, second.Id, 0.10m, 0.05m), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(fixture.Orders.Orders);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(0.10m, order.Lines[0].DutyRate);
        Assert.Equal(0.05m, order.Lines[1].DutyRate);
        Assert.Equal(0.08m, order.VatRate);
        Assert.Equal(fixture.Location.Id, order.ReceiveLocationId);
    }

    [Fact]
    public async Task MissingLine_DoesNotInsert()
    {
        var fixture = new PurchaseFixture();
        var command = Command(fixture, Guid.CreateVersion7(), Guid.CreateVersion7(), 0.1m, 0.1m);
        command.Lines = [];

        var result = await fixture.Create.HandleAsync(command, CancellationToken.None);

        Assert.Equal(PurchaseError.InvalidOrder, result.PurchaseError);
        Assert.Empty(fixture.Orders.Orders);
    }

    [Fact]
    public async Task ALineWithoutAFobPrice_IsRejected_InsteadOfSilentlyBreakingTheChargeSplit()
    {
        var fixture = new PurchaseFixture();
        var item = Item("SP01", 2m);
        fixture.Catalog.Items.Add(item);

        var result = await fixture.Create.HandleAsync(Command(fixture, item.Id, item.Id, 0.1m, 0.1m, firstFob: 0m), CancellationToken.None);

        Assert.Equal(PurchaseError.ZeroPrice, result.PurchaseError);
        Assert.Empty(fixture.Orders.Orders);
    }

    [Fact]
    public async Task ZeroQuantity_DoesNotStoreTheLine()
    {
        var fixture = new PurchaseFixture();
        var item = Item("SP01", 2m);
        fixture.Catalog.Items.Add(item);
        var command = Command(fixture, item.Id, item.Id, 0.1m, 0.1m);
        command.Lines = [new CreatePurchaseLineInput { CatalogItemId = item.Id, Quantity = 0, FobUsd = 10m, DutyRate = 0.1m }];

        var result = await fixture.Create.HandleAsync(command, CancellationToken.None);

        Assert.Equal(PurchaseError.InvalidOrder, result.PurchaseError);
        Assert.Empty(fixture.Orders.Orders);
    }

    [Fact]
    public async Task ChargeForty_SplitsByFobQuantityAndWeight()
    {
        var fixture = new PurchaseFixture();
        var light = Item("SP01", 2m);
        var heavy = Item("SP02", 6m);
        fixture.Catalog.Items.Add(light);
        fixture.Catalog.Items.Add(heavy);
        var created = await fixture.Create.HandleAsync(Command(fixture, light.Id, heavy.Id, 0.1m, 0.1m, 100m, 300m, 1, 1), CancellationToken.None);
        var order = fixture.Orders.Orders[0];

        var byFob = await fixture.Allocate.HandleAsync(Charge(order.Id, ChargeSplitMethod.FobValue), CancellationToken.None);
        Assert.True(byFob.IsSuccess);
        AssertShares(order, 10m, 30m);

        order.DropCharge(order.Charges[0].Id);
        var byQuantity = await fixture.CreateQuantityOrder(fixture, light, heavy);
        var quantityOrder = fixture.Orders.Orders.Single(item => item.Id == byQuantity);
        var quantityCharge = await fixture.Allocate.HandleAsync(Charge(quantityOrder.Id, ChargeSplitMethod.Quantity), CancellationToken.None);
        Assert.True(quantityCharge.IsSuccess);
        AssertShares(quantityOrder, 10m, 30m);

        var weightCharge = await fixture.Allocate.HandleAsync(Charge(order.Id, ChargeSplitMethod.Weight), CancellationToken.None);
        Assert.True(weightCharge.IsSuccess);
        AssertShares(order, 10m, 30m);
        Assert.Equal(created.Value, order.Id);
    }

    [Fact]
    public async Task MissingWeight_DoesNotWriteShares()
    {
        var fixture = new PurchaseFixture();
        var known = Item("SP01", 2m);
        var missing = Item("SP02", null);
        fixture.Catalog.Items.Add(known);
        fixture.Catalog.Items.Add(missing);
        await fixture.Create.HandleAsync(Command(fixture, known.Id, missing.Id, 0.1m, 0.1m), CancellationToken.None);
        var order = fixture.Orders.Orders[0];

        var result = await fixture.Allocate.HandleAsync(Charge(order.Id, ChargeSplitMethod.Weight), CancellationToken.None);

        Assert.Equal(PurchaseError.MissingMeasure, result.PurchaseError);
        Assert.Empty(order.Charges);
    }

    [Fact]
    public async Task UngroupedVatCharge_IsNotLandedCost()
    {
        var fixture = new PurchaseFixture();
        var item = Item("SP01", 2m);
        fixture.Catalog.Items.Add(item);
        var command = Command(fixture, item.Id, item.Id, 0m, 0m, 100m, 100m, 1, 1);
        command.ExchangeRate = 1m;
        command.VatDeductible = true;
        await fixture.Create.HandleAsync(command, CancellationToken.None);
        var order = fixture.Orders.Orders[0];
        Assert.True(order.TryAddCharge(13m, ChargeSplitMethod.Manual, out var vat));
        Assert.True(order.TryAllocate(vat, new Dictionary<Guid, decimal>
        {
            [order.Lines[0].Id] = 6m,
            [order.Lines[1].Id] = 7m
        }, true));

        var cost = PurchaseCosting.For(order, order.Lines[0]);

        Assert.Null(order.Charges[0].Group);
        Assert.Equal(100m, cost.LandedCost);
        Assert.DoesNotContain(order.Charges, charge => charge.Group == ChargeGroup.DeductibleTax);
    }

    private static void AssertShares(PurchaseOrder order, decimal first, decimal second)
    {
        var shares = order.Charges.Last().Shares.OrderBy(share => share.Amount).ToArray();
        Assert.Equal(first, shares[0].Amount);
        Assert.Equal(second, shares[1].Amount);
    }

    private static AllocateChargeCommand Charge(Guid orderId, ChargeSplitMethod method) =>
        new() { PurchaseOrderId = orderId, Amount = 40m, Method = method };

    private static CatalogItem Item(string code, decimal? weight) =>
        CatalogItem.Create(code, code, ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", weight, null, null, null, 1m, 1m, 1m, 1m, 1m);

    private static CreatePurchaseOrderCommand Command(
        PurchaseFixture fixture,
        Guid first,
        Guid second,
        decimal firstDuty,
        decimal secondDuty,
        decimal firstFob = 10m,
        decimal secondFob = 20m,
        int firstQuantity = 2,
        int secondQuantity = 3) =>
        new()
        {
            Code = "PO-" + Guid.NewGuid().ToString("N")[..6],
            SupplierId = fixture.Supplier.Id,
            ReceiveLocationId = fixture.Location.Id,
            ExchangeRate = 25000m,
            VatRate = 0.08m,
            Lines =
            [
                new CreatePurchaseLineInput { CatalogItemId = first, Quantity = firstQuantity, FobUsd = firstFob, DutyRate = firstDuty },
                new CreatePurchaseLineInput { CatalogItemId = second, Quantity = secondQuantity, FobUsd = secondFob, DutyRate = secondDuty }
            ]
        };

    private sealed class PurchaseFixture
    {
        public PurchaseFixture()
        {
            Supplier = Supplier.Create("NCC1", "Nhà A", "Việt Nam", "An", "0900000000", "a@b.co", "TK");
            Location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
            Suppliers.Suppliers.Add(Supplier);
            Inventory.Locations.Add(Location);
            Create = new CreatePurchaseOrderCommandHandler(Orders, Suppliers, Inventory, Catalog, NullLogger<CreatePurchaseOrderCommandHandler>.Instance);
            Allocate = new AllocateChargeCommandHandler(
                Orders,
                new GetCatalogMeasuresHandler(Catalog, NullLogger<GetCatalogMeasuresHandler>.Instance),
                NullLogger<AllocateChargeCommandHandler>.Instance);
        }

        public FakePurchaseStore Orders { get; } = new();

        public FakeSupplierRepository Suppliers { get; } = new();

        public FakeInventoryStore Inventory { get; } = new();

        public FakeCatalogRepository Catalog { get; } = new();

        public Supplier Supplier { get; }

        public StockLocation Location { get; }

        public CreatePurchaseOrderCommandHandler Create { get; }

        public AllocateChargeCommandHandler Allocate { get; }

        public async Task<Guid> CreateQuantityOrder(PurchaseFixture fixture, CatalogItem light, CatalogItem heavy)
        {
            var command = Command(fixture, light.Id, heavy.Id, 0.1m, 0.1m, 100m, 300m, 1, 3);
            var result = await Create.HandleAsync(command, CancellationToken.None);
            return result.Value;
        }
    }
}
