using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Purchasing;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class ListPurchaseOrdersHandlerTests
{
    [Fact]
    public async Task List_ReturnsSupplierContainerAndLocation()
    {
        var orders = new FakePurchaseStore();
        var suppliers = new FakeSupplierRepository();
        var inventory = new FakeInventoryStore();
        var supplier = Supplier.Create("NCC1", "Nhà A", "Việt Nam", "An", "0900000000", "a@b.co", "TK");
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        suppliers.Suppliers.Add(supplier);
        inventory.Locations.Add(location);
        var order = PurchaseOrder.TryCreate(
            "PO-1",
            supplier.Id,
            location.Id,
            "CONT1",
            "BL1",
            "Hải Phòng",
            new DateOnly(2026, 11, 12),
            25000m,
            0.10m,
            [(Guid.CreateVersion7(), 2, 10m, 0.05m, 0m, 0m, 0m)])!;
        orders.Orders.Add(order);
        var handler = new ListPurchaseOrdersHandler(orders, suppliers, inventory, NullLogger<ListPurchaseOrdersHandler>.Instance);

        var list = await handler.HandleAsync(TestActors.Accountant, CancellationToken.None);

        var row = Assert.Single(list);
        Assert.Equal("PO-1", row.Code);
        Assert.Equal(1, row.LineCount);
        Assert.Equal("Nhà A", row.SupplierName);
        Assert.Equal("CONT1", row.ContainerNumber);
        Assert.Equal("Kho A", row.ReceiveLocationName);
    }

    [Fact]
    public async Task EmptyList_DoesNotFail()
    {
        var handler = new ListPurchaseOrdersHandler(
            new FakePurchaseStore(),
            new FakeSupplierRepository(),
            new FakeInventoryStore(),
            NullLogger<ListPurchaseOrdersHandler>.Instance);

        var list = await handler.HandleAsync(TestActors.Accountant, CancellationToken.None);

        Assert.Empty(list);
    }

    [Fact]
    public async Task Get_ReturnsLinesAndCharges()
    {
        var orders = new FakePurchaseStore();
        var suppliers = new FakeSupplierRepository();
        var inventory = new FakeInventoryStore();
        var supplier = Supplier.Create("NCC1", "Nhà A", "Việt Nam", "An", "0900000000", "a@b.co", "TK");
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var itemId = Guid.CreateVersion7();
        suppliers.Suppliers.Add(supplier);
        inventory.Locations.Add(location);
        inventory.Items.Add(new StockItemInfo(itemId, "SP01", "Vòi", "Toto", Domain.Catalog.ProductCategory.SanitaryEquipment));
        var order = PurchaseOrder.TryCreate(
            "PO-1",
            supplier.Id,
            location.Id,
            "CONT1",
            string.Empty,
            string.Empty,
            null,
            1m,
            0.10m,
            [(itemId, 1, 100m, 0.10m, 0m, 0m, 0m), (Guid.CreateVersion7(), 1, 50m, 0.05m, 0m, 0m, 0m)])!;
        Assert.True(order.TryAddCharge(20m, ChargeSplitMethod.FobValue, out var chargeId));
        Assert.True(order.TryAllocate(chargeId, order.Lines.ToDictionary(line => line.Id, line => line.FobUsd), false));
        orders.Orders.Add(order);
        var handler = new GetPurchaseOrderHandler(orders, suppliers, inventory, NullLogger<GetPurchaseOrderHandler>.Instance);

        var detail = await handler.HandleAsync(order.Id, TestActors.Accountant, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(2, detail.Lines.Count);
        Assert.Equal("SP01", detail.Lines[0].ItemCode);
        var charge = Assert.Single(detail.Charges);
        Assert.Equal(2, charge.Shares.Count);
        Assert.Equal("Nhà A", detail.SupplierName);
        Assert.Equal("Kho A", detail.ReceiveLocationName);
    }
}
