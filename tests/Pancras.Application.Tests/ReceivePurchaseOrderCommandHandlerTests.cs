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
        var handler = new ReceivePurchaseOrderCommandHandler(orders, inventory, catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance);

        var first = await handler.HandleAsync(new ReceivePurchaseOrderCommand(order.Id), CancellationToken.None);
        var stored = await orders.FindReadOnlyAsync(order.Id, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(PurchaseStatus.Received, stored!.Status);
        Assert.Equal(7, here.OnHand);
        Assert.Equal(2, here.InTransit);
        Assert.Equal(7, there.OnHand);
        Assert.Equal(3, there.InTransit);
        Assert.Equal(8m, item.CostPrice);
        Assert.Equal(1m, stored.ExchangeRate);

        var second = await handler.HandleAsync(new ReceivePurchaseOrderCommand(order.Id), CancellationToken.None);

        Assert.Equal(PurchaseError.AlreadyReceived, second.PurchaseError);
        Assert.Equal(7, here.OnHand);
        Assert.Equal(8m, item.CostPrice);
        Assert.Equal(1m, stored.ExchangeRate);
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

        var result = await new ReceivePurchaseOrderCommandHandler(orders, inventory, catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance)
            .HandleAsync(new ReceivePurchaseOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(33m, item.CostPrice);
    }
}
