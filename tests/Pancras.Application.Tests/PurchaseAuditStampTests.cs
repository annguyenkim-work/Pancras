using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Auditing;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

internal sealed class FakeRequestInfo(string code) : IRequestInfo
{
    public string? IpAddress => "127.0.0.1";

    public Guid? ActorId => Guid.Empty;

    public string? ActorCode => code;
}

/// <summary>Đơn mua ghi lại ai tạo, ai sửa lần cuối, ai nhập kho và lúc nào, ngay trên bản ghi.</summary>
public class PurchaseAuditStampTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Domain_RecordsCreatedUpdatedAndReceived()
    {
        var order = PurchaseOrder.TryCreate("PO-S", Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 25000m, 0m, [(Guid.CreateVersion7(), 1, 10m, 0m, 0m, 0m, 0m)], true, T0, "AnNK")!;

        Assert.Equal(T0, order.CreatedAt);
        Assert.Equal("AnNK", order.CreatedBy);
        Assert.Null(order.UpdatedAt);
        Assert.Null(order.ReceivedAt);

        order.StampUpdated(T0.AddHours(1), "Kế toán 1");
        Assert.Equal(T0.AddHours(1), order.UpdatedAt);
        Assert.Equal("Kế toán 1", order.UpdatedBy);

        var undo = order.ReceiveAll(T0.AddHours(2), "Thủ kho");
        Assert.Equal(T0.AddHours(2), order.ReceivedAt);
        Assert.Equal("Thủ kho", order.ReceivedBy);
        Assert.Equal(T0.AddHours(2), order.UpdatedAt);

        order.UndoReceipt(undo);
        Assert.Null(order.ReceivedAt);
        Assert.Equal(string.Empty, order.ReceivedBy);
    }

    [Fact]
    public void Domain_WithoutATime_StillGetsACreationTime_AndTrimsTheActor()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var order = PurchaseOrder.TryCreate("PO-D", Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 25000m, 0m, [(Guid.CreateVersion7(), 1, 10m, 0m, 0m, 0m, 0m)], true, null, new string('x', 100))!;

        Assert.True(order.CreatedAt > before);
        Assert.Equal(PurchaseOrder.ActorMaxLength, order.CreatedBy.Length);
    }

    [Fact]
    public async Task CreatingAnOrder_StoresWhoAndWhen()
    {
        var supplier = Supplier.Create("NCC1", "Nhà A", "Việt Nam", "An", "0900000000", "a@b.co", "TK");
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var item = CatalogItem.Create("SP01", "Vòi", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 2m, null, null, null, 1m, 1m, 1m, 1m, 1m);
        var suppliers = new FakeSupplierRepository();
        suppliers.Suppliers.Add(supplier);
        var inventory = new FakeInventoryStore();
        inventory.Locations.Add(location);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var orders = new FakePurchaseStore();
        var handler = new CreatePurchaseOrderCommandHandler(
            orders, suppliers, inventory, catalog, NullLogger<CreatePurchaseOrderCommandHandler>.Instance,
            null, new TestClock(T0), new FakeRequestInfo("AnNK"));

        var result = await handler.HandleAsync(
            new CreatePurchaseOrderCommand
            {
                Actor = TestActors.Accountant,
                Code = "PO-1",
                SupplierId = supplier.Id,
                ReceiveLocationId = location.Id,
                ExchangeRate = 25000m,
                Lines = [new CreatePurchaseLineInput { CatalogItemId = item.Id, Quantity = 1, FobUsd = 10m }]
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = orders.Orders.Single();
        Assert.Equal(T0, order.CreatedAt);
        Assert.Equal("AnNK", order.CreatedBy);
        Assert.Null(order.UpdatedAt);
    }

    [Fact]
    public async Task ReceivingAnOrder_StoresWhoAndWhen_AndCountsAsAnUpdate()
    {
        var item = CatalogItem.Create("SP05", "Sen", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", null, null, null, null, 9m, 1m, 1m, 1m, 1m);
        var warehouse = StockLocation.Create("Kho E", StockLocationKind.Warehouse);
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(item);
        var orders = new FakePurchaseStore();
        var order = PurchaseOrder.TryCreate("PO-R", Guid.CreateVersion7(), warehouse.Id, "CONT", null, null, null, 1m, 0m, [(item.Id, 2, 50m, 0m, 0m, 0m, 0m)], true, T0, "AnNK")!;
        orders.Orders.Add(order);
        order.Approve();
        var handler = new ReceivePurchaseOrderCommandHandler(
            orders, new FakeInventoryStore(), catalog, NullLogger<ReceivePurchaseOrderCommandHandler>.Instance,
            new TestClock(T0.AddDays(1)), null, new FakeRequestInfo("Thukho1"));

        var result = await handler.HandleAsync(new ReceivePurchaseOrderCommand(order.Id, TestActors.Accountant), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(T0.AddDays(1), order.ReceivedAt);
        Assert.Equal("Thukho1", order.ReceivedBy);
        Assert.Equal(T0.AddDays(1), order.UpdatedAt);
        Assert.Equal("AnNK", order.CreatedBy);
    }
}
