using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Purchasing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

public class GetPurchaseReceiptPreviewHandlerTests
{
    private sealed record Setup(
        CatalogItem Item,
        PurchaseOrder Order,
        FakeCatalogRepository Catalog,
        FakeInventoryStore Inventory,
        FakePurchaseStore Orders,
        StockLocation Warehouse);

    private static Setup Arrange()
    {
        var item = CatalogItem.Create("SP01", "Vòi", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 2m, null, null, null, 1m, 1m, 1m, 1m, 1m);
        var warehouse = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var showroom = StockLocation.Create("Showroom", StockLocationKind.Showroom);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var inventory = new FakeInventoryStore();
        inventory.Balances.Add(StockBalance.Create(item.Id, warehouse.Id, 4, 1, 2));
        inventory.Balances.Add(StockBalance.Create(item.Id, showroom.Id, 7, 0, 3));
        var orders = new FakePurchaseStore();
        var order = PurchaseOrder.TryCreate(
            "PO-1", Guid.CreateVersion7(), warehouse.Id, "CONT", null, null, null, 1m, 0.10m,
            [(item.Id, 3, 100m, 0m, 0m, 0m, 0m)])!;
        orders.Orders.Add(order);
        order.Approve();
        return new Setup(item, order, catalog, inventory, orders, warehouse);
    }

    [Fact]
    public async Task Preview_MatchesTheCostActuallyWritten_WhenTheWholeRemainderIsReceived()
    {
        var s = Arrange();
        var preview = new GetPurchaseReceiptPreviewHandler(s.Orders, s.Catalog, s.Inventory);
        var receive = new ReceivePurchaseOrderCommandHandler(s.Orders, s.Inventory, s.Catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance);
        var before = s.Item.CostPrice;

        var lines = await preview.HandleAsync(s.Order.Id, TestActors.Accountant, CancellationToken.None);
        var received = await receive.HandleAsync(new ReceivePurchaseOrderCommand(s.Order.Id, TestActors.Accountant), CancellationToken.None);

        Assert.True(received.IsSuccess);
        var line = Assert.Single(lines);
        Assert.Equal(s.Order.Lines[0].Id, line.LineId);
        Assert.Equal(3, line.Remaining);
        Assert.Equal(11, line.OnHand);
        Assert.Equal(before, line.CurrentCost);
        Assert.Equal(s.Item.CostPrice, line.ProjectedCost);
        Assert.NotEqual(line.CurrentCost, line.ProjectedCost);
    }

    [Fact]
    public async Task Preview_DoesNotChangeStockOrCost()
    {
        var s = Arrange();
        var preview = new GetPurchaseReceiptPreviewHandler(s.Orders, s.Catalog, s.Inventory);
        var before = s.Item.CostPrice;

        await preview.HandleAsync(s.Order.Id, TestActors.Admin, CancellationToken.None);

        Assert.Equal(before, s.Item.CostPrice);
        Assert.Equal(11, await s.Inventory.SumOnHandAsync(s.Item.Id, CancellationToken.None));
        Assert.Equal(PurchaseStatus.Approved, s.Order.Status);
    }

    [Fact]
    public async Task Preview_IsEmpty_ForRolesThatCannotSeeCost()
    {
        var s = Arrange();
        var preview = new GetPurchaseReceiptPreviewHandler(s.Orders, s.Catalog, s.Inventory);

        var keeper = await preview.HandleAsync(s.Order.Id, TestActors.Keeper(s.Warehouse.Id), CancellationToken.None);
        var sales = await preview.HandleAsync(s.Order.Id, TestActors.Sales(s.Warehouse.Id), CancellationToken.None);

        Assert.Empty(keeper);
        Assert.Empty(sales);
    }

    [Fact]
    public async Task Preview_IsEmpty_WhenTheOrderCannotBeReceived()
    {
        var s = Arrange();
        var preview = new GetPurchaseReceiptPreviewHandler(s.Orders, s.Catalog, s.Inventory);
        var receive = new ReceivePurchaseOrderCommandHandler(s.Orders, s.Inventory, s.Catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance);
        await receive.HandleAsync(new ReceivePurchaseOrderCommand(s.Order.Id, TestActors.Accountant), CancellationToken.None);

        var afterReceipt = await preview.HandleAsync(s.Order.Id, TestActors.Accountant, CancellationToken.None);
        var missing = await preview.HandleAsync(Guid.NewGuid(), TestActors.Accountant, CancellationToken.None);

        Assert.Empty(afterReceipt);
        Assert.Empty(missing);
    }
}
