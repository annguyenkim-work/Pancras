using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

/// <summary>Đơn mua giữ đúng tên nhà cung cấp tại lúc lập đơn, dù hồ sơ nhà cung cấp đổi sau đó.</summary>
public class SupplierSnapshotOnOrdersTests
{
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakePurchaseStore _orders = new();

    [Fact]
    public async Task RenamingTheSupplier_DoesNotChangeTheNameOnAnExistingOrder()
    {
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "", "", "");
        _suppliers.Suppliers.Add(supplier);
        var order = NewOrder(supplier.Id);
        order.RecordSupplier(supplier.Code, supplier.Name);
        _orders.Orders.Add(order);

        supplier.Update("NCC99", "Tên mới", "Trung Quốc", "Nguyễn An", "", "", "");

        var list = await new ListPurchaseOrdersHandler(_orders, _suppliers, new FakeInventoryStore(), NullLogger<ListPurchaseOrdersHandler>.Instance)
            .HandleAsync(SupplierActors.Admin, null, CancellationToken.None);
        Assert.Equal("Kim An", Assert.Single(list.Items).SupplierName);
        Assert.Equal("NCC01", order.SupplierCode);
    }

    [Fact]
    public async Task AnOldOrderWithoutASnapshot_FallsBackToTheCurrentName()
    {
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "", "", "");
        _suppliers.Suppliers.Add(supplier);
        _orders.Orders.Add(NewOrder(supplier.Id));

        var list = await new ListPurchaseOrdersHandler(_orders, _suppliers, new FakeInventoryStore(), NullLogger<ListPurchaseOrdersHandler>.Instance)
            .HandleAsync(SupplierActors.Admin, null, CancellationToken.None);

        Assert.Equal("Kim An", Assert.Single(list.Items).SupplierName);
    }

    private static PurchaseOrder NewOrder(Guid supplierId) =>
        PurchaseOrder.TryCreate("PO-1", supplierId, Guid.CreateVersion7(), null, null, null, null, 25000m, 0m, [(Guid.CreateVersion7(), 1, 10m, 0m, 0m, 0m, 0m)])!;
}
