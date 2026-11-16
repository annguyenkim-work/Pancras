using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

public class ReceivePurchaseOrderCommandHandlerTests
{
    [Fact]
    public async Task Receive_Once_UpdatesOnHandAndOneCompanyCost()
    {
        var item = CatalogItem.Create("SP01", "Vòi", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 2m, null, null, null, 1m, 1m, 1m, 1m, 1m);
        var warehouse = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var showroom = StockLocation.Create("Showroom", StockLocationKind.Showroom);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var inventory = new FakeInventoryStore();
        var here = StockBalance.Create(item.Id, warehouse.Id, 4, 1, 2);
        var there = StockBalance.Create(item.Id, showroom.Id, 7, 0, 3);
        inventory.Balances.Add(here);
        inventory.Balances.Add(there);
        var orders = new FakePurchaseStore();
        var order = PurchaseOrder.TryCreate(
            "PO-1",
            Guid.CreateVersion7(),
            warehouse.Id,
            "CONT",
            null,
            null,
            null,
            1m,
            0.10m,
            [(item.Id, 3, 100m, 0m, 0m, 0m, 0m)])!;
        orders.Orders.Add(order);
        order.Approve();
        var handler = new ReceivePurchaseOrderCommandHandler(orders, inventory, catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance);

        var first = await handler.HandleAsync(new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant), CancellationToken.None);
        var stored = await orders.FindReadOnlyAsync(order.Id, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(PurchaseStatus.Received, stored!.Status);
        Assert.Equal(7, here.OnHand);
        Assert.Equal(2, here.InTransit);
        Assert.Equal(7, there.OnHand);
        Assert.Equal(3, there.InTransit);
        Assert.Equal(8m, item.CostPrice);
        Assert.Equal(1m, stored.ExchangeRate);

        var second = await handler.HandleAsync(new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant), CancellationToken.None);

        Assert.Equal(PurchaseError.AlreadyReceived, second.Error);
        Assert.Equal(7, here.OnHand);
        Assert.Equal(8m, item.CostPrice);
        Assert.Equal(1m, stored.ExchangeRate);
    }

    [Fact]
    public async Task Receive_IsBlocked_WhileAChargeHasNoGroup()
    {
        var item = CatalogItem.Create("SP03", "Bồn", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", null, null, null, null, 9m, 1m, 1m, 1m, 1m);
        var warehouse = StockLocation.Create("Kho C", StockLocationKind.Warehouse);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var inventory = new FakeInventoryStore();
        var orders = new FakePurchaseStore();
        var order = PurchaseOrder.TryCreate("PO-3", Guid.CreateVersion7(), warehouse.Id, "CONT", null, null, null, 1m, 0m, [(item.Id, 2, 50m, 0m, 0m, 0m, 0m)])!;
        Assert.True(order.TryAddCharge(20m, ChargeSplitMethod.FobValue, out var chargeId, "THC"));
        Assert.True(order.TryAllocate(chargeId, new Dictionary<Guid, decimal> { [order.Lines[0].Id] = 100m }, false));
        orders.Orders.Add(order);
        order.Approve();
        var receive = new ReceivePurchaseOrderCommandHandler(orders, inventory, catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance);

        var blocked = await receive.HandleAsync(new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant), CancellationToken.None);
        Assert.True(order.TryAssignGroup(chargeId, ChargeGroup.Logistics, false));
        var received = await receive.HandleAsync(new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant), CancellationToken.None);

        Assert.Equal(PurchaseError.UngroupedCharge, blocked.Error);
        Assert.True(received.IsSuccess);
        Assert.Equal(35m, item.CostPrice);
        Assert.Equal(ChargeGroup.Logistics, order.Charges[0].Group);
    }

    [Fact]
    public async Task Receive_IsRefused_ForALineWithoutAPrice_AndTheCompanyCostIsUntouched()
    {
        var item = CatalogItem.Create("SP04", "Gương", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", null, null, null, null, 500m, 1m, 1m, 1m, 1m);
        var warehouse = StockLocation.Create("Kho D", StockLocationKind.Warehouse);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var inventory = new FakeInventoryStore();
        inventory.Balances.Add(StockBalance.Create(item.Id, warehouse.Id, 10, 0, 0));
        var orders = new FakePurchaseStore();
        var order = PurchaseOrder.TryCreate("PO-0", Guid.CreateVersion7(), warehouse.Id, "CONT", null, null, null, 25000m, 0m, [(item.Id, 10, 0m, 0m, 0m, 0m, 0m)])!;
        orders.Orders.Add(order);
        order.Approve();

        var result = await new ReceivePurchaseOrderCommandHandler(orders, inventory, catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance)
            .HandleAsync(new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant), CancellationToken.None);

        Assert.Equal(PurchaseError.ZeroPrice, result.Error);
        Assert.Equal(PurchaseStatus.Approved, order.Status);
        Assert.Equal(500m, item.CostPrice);
    }

    [Fact]
    public async Task Receive_WhenStockIsEmpty_UsesTheNewUnitCost()
    {
        var item = CatalogItem.Create("SP02", "Sen", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", null, null, null, null, 9m, 1m, 1m, 1m, 1m);
        var warehouse = StockLocation.Create("Kho B", StockLocationKind.Warehouse);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var inventory = new FakeInventoryStore();
        var orders = new FakePurchaseStore();
        var order = PurchaseOrder.TryCreate(
            "PO-2",
            Guid.CreateVersion7(),
            warehouse.Id,
            "CONT",
            null,
            null,
            null,
            1m,
            0m,
            [(item.Id, 3, 100m, 0m, 0m, 0m, 0m)])!;
        orders.Orders.Add(order);
        order.Approve();

        var result = await new ReceivePurchaseOrderCommandHandler(orders, inventory, catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance)
            .HandleAsync(new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(33m, item.CostPrice);
    }

    [Fact]
    public async Task Receive_SixOfTen_StaysPartial_ThenTheRestClosesTheOrder()
    {
        var item = CatalogItem.Create("SP10", "Chậu", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", null, null, null, null, 9m, 1m, 1m, 1m, 1m);
        var warehouse = StockLocation.Create("Kho E", StockLocationKind.Warehouse);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var inventory = new FakeInventoryStore();
        var orders = new FakePurchaseStore();
        var order = PurchaseOrder.TryCreate("PO-10", Guid.CreateVersion7(), warehouse.Id, "CONT", null, null, null, 1m, 0m, [(item.Id, 10, 100m, 0m, 0m, 0m, 0m)])!;
        orders.Orders.Add(order);
        order.Approve();
        var handler = new ReceivePurchaseOrderCommandHandler(orders, inventory, catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance);
        var lineId = order.Lines[0].Id;

        var shortWithoutReason = await handler.HandleAsync(
            new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant, new Dictionary<Guid, int> { [lineId] = 6 }, true),
            CancellationToken.None);
        Assert.Equal(PurchaseError.ReceiptNoteRequired, shortWithoutReason.Error);
        Assert.Equal(PurchaseStatus.Approved, order.Status);

        var partial = await handler.HandleAsync(
            new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant, new Dictionary<Guid, int> { [lineId] = 6 }, false),
            CancellationToken.None);
        Assert.True(partial.IsSuccess);
        Assert.Equal(PurchaseStatus.PartiallyReceived, order.Status);
        Assert.Equal(6, order.Lines[0].ReceivedQuantity);

        var rest = await handler.HandleAsync(
            new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant, new Dictionary<Guid, int> { [lineId] = 4 }, true),
            CancellationToken.None);
        Assert.True(rest.IsSuccess);
        Assert.Equal(PurchaseStatus.Received, order.Status);
        Assert.Equal(10, order.Lines[0].ReceivedQuantity);
    }
}
