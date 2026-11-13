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
    public async Task InitialCharges_AreSavedTogetherWithTheOrder()
    {
        var fixture = new PurchaseFixture();
        var item = Item("SP01", 2m);
        fixture.Catalog.Items.Add(item);
        var command = Command(fixture, item.Id, item.Id, 0.1m, 0.1m);
        command.Charges =
        [
            new CreatePurchaseChargeInput { Amount = 1000m, Label = "THC", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.FobValue },
            new CreatePurchaseChargeInput { Amount = 0m, Label = "Bỏ qua", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.Quantity }
        ];

        var result = await fixture.Create.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(fixture.Orders.Orders);
        var charge = Assert.Single(order.Charges);
        Assert.Equal("THC", charge.Label);
        Assert.Equal(ChargeGroup.Logistics, charge.Group);
        Assert.Equal(1000m, charge.Shares.Sum(share => share.Amount));
        Assert.False(order.HasUngroupedCharges);
    }

    [Fact]
    public async Task AnInitialChargeSplitByVolume_IsNotSilentlySwitched_WhenTheMeasureIsMissing()
    {
        var fixture = new PurchaseFixture();
        var item = Item("SP01", 2m);
        fixture.Catalog.Items.Add(item);
        var command = Command(fixture, item.Id, item.Id, 0.1m, 0.1m);
        command.Charges = [new CreatePurchaseChargeInput { Amount = 700m, Label = "Cước", Group = ChargeGroup.CustomsValueAdjustment, Method = ChargeSplitMethod.Volume }];

        var result = await fixture.Create.HandleAsync(command, CancellationToken.None);

        Assert.Equal(PurchaseError.ChargeFailed, result.PurchaseError);
        Assert.Empty(fixture.Orders.Orders);
    }

    [Fact]
    public async Task IfAnInitialChargeCannotBeAdded_NoOrderIsSaved()
    {
        var fixture = new PurchaseFixture();
        var item = Item("SP01", 2m);
        fixture.Catalog.Items.Add(item);
        var command = Command(fixture, item.Id, item.Id, 0.1m, 0.1m);
        command.Charges = [new CreatePurchaseChargeInput { Amount = 500m, Label = "Sai nhóm", Group = (ChargeGroup)99 }];

        var result = await fixture.Create.HandleAsync(command, CancellationToken.None);

        Assert.Equal(PurchaseError.ChargeFailed, result.PurchaseError);
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

        // Mỗi cách chia một đơn riêng; khoản 40 đồng chia cho hai dòng theo tỷ lệ 1:3.
        var byFob = await CreateWithChargeAsync(fixture, light, heavy, ChargeSplitMethod.FobValue, firstFob: 100m, secondFob: 300m, firstQuantity: 1, secondQuantity: 1);
        var byQuantity = await CreateWithChargeAsync(fixture, light, heavy, ChargeSplitMethod.Quantity, firstFob: 100m, secondFob: 300m, firstQuantity: 1, secondQuantity: 3);
        var byWeight = await CreateWithChargeAsync(fixture, light, heavy, ChargeSplitMethod.Weight, firstFob: 100m, secondFob: 300m, firstQuantity: 1, secondQuantity: 1);

        AssertShares(byFob, 10m, 30m);
        AssertShares(byQuantity, 10m, 30m);
        AssertShares(byWeight, 10m, 30m);
    }

    [Fact]
    public async Task MissingWeight_RejectsTheOrder_InsteadOfWritingShares()
    {
        var fixture = new PurchaseFixture();
        var known = Item("SP01", 2m);
        var missing = Item("SP02", null);
        fixture.Catalog.Items.Add(known);
        fixture.Catalog.Items.Add(missing);
        var command = Command(fixture, known.Id, missing.Id, 0.1m, 0.1m);
        command.Charges = [new CreatePurchaseChargeInput { Amount = 40m, Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.Weight }];

        var result = await fixture.Create.HandleAsync(command, CancellationToken.None);

        Assert.Equal(PurchaseError.ChargeFailed, result.PurchaseError);
        Assert.Empty(fixture.Orders.Orders);
    }

    private static async Task<PurchaseOrder> CreateWithChargeAsync(
        PurchaseFixture fixture,
        CatalogItem first,
        CatalogItem second,
        ChargeSplitMethod method,
        decimal firstFob,
        decimal secondFob,
        int firstQuantity,
        int secondQuantity)
    {
        var command = Command(fixture, first.Id, second.Id, 0.1m, 0.1m, firstFob, secondFob, firstQuantity, secondQuantity);
        command.Charges = [new CreatePurchaseChargeInput { Amount = 40m, Label = "Phí", Group = ChargeGroup.Logistics, Method = method }];
        var result = await fixture.Create.HandleAsync(command, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return fixture.Orders.Orders.Single(order => order.Id == result.Value);
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
            Create = new CreatePurchaseOrderCommandHandler(Orders, Suppliers, Inventory, Catalog, NullLogger<CreatePurchaseOrderCommandHandler>.Instance, new GetCatalogMeasuresHandler(Catalog, NullLogger<GetCatalogMeasuresHandler>.Instance));
        }

        public FakePurchaseStore Orders { get; } = new();

        public FakeSupplierRepository Suppliers { get; } = new();

        public FakeInventoryStore Inventory { get; } = new();

        public FakeCatalogRepository Catalog { get; } = new();

        public Supplier Supplier { get; }

        public StockLocation Location { get; }

        public CreatePurchaseOrderCommandHandler Create { get; }
    }
}
