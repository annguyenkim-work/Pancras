using Microsoft.Extensions.Logging;
using Pancras.Application.Inventory;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class ListPurchaseOrdersHandler(
    IPurchaseOrderRepository orders,
    ISupplierRepository suppliers,
    IInventoryRepository inventory,
    ILogger<ListPurchaseOrdersHandler> logger)
{
    /// <summary>Vai trò quản lý thấy mọi đơn kèm giá vốn. Kho chỉ thấy đơn đã duyệt trở đi của kho mình, không có số tiền.</summary>
    public async Task<IReadOnlyList<PurchaseOrderSummary>> HandleAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var costVisible = PurchaseAccess.CanViewCost(actor);
        var list = (await orders.ListAsync(cancellationToken))
            .Where(order => PurchaseAccess.CanView(actor, order.ReceiveLocationId, order.Status))
            .ToArray();
        // Ưu tiên tên đã chụp lúc lập đơn; đơn cũ chưa có bản chụp thì lấy tên hiện tại (kể cả nhà cung cấp đã ngừng hợp tác).
        var supplierNames = await suppliers.GetNamesAsync(list.Select(order => order.SupplierId).Distinct().ToArray(), cancellationToken);
        var locations = (await inventory.ListLocationsAsync(cancellationToken)).ToDictionary(location => location.Id, location => location.Name);
        logger.LogInformation("Listed {PurchaseOrderCount} purchase orders.", list.Length);
        return list
            .OrderByDescending(order => order.Id)
            .Select(order => new PurchaseOrderSummary(
                order.Id,
                order.Code,
                order.Lines.Count,
                PurchaseSupplierNames.Of(order, supplierNames),
                order.ContainerNumber,
                order.BillOfLading,
                order.Port,
                order.ExpectedOn,
                locations.GetValueOrDefault(order.ReceiveLocationId, string.Empty),
                order.Status,
                costVisible ? order.Lines.Sum(line => PurchaseCosting.For(order, line).LandedCost) : 0,
                costVisible ? order.ExchangeRate : 0,
                costVisible,
                order.SupplierId))
            .ToArray();
    }
}

internal static class PurchaseSupplierNames
{
    public static string Of(PurchaseOrder order, IReadOnlyDictionary<Guid, string> current) =>
        order.SupplierName.Length > 0 ? order.SupplierName : current.GetValueOrDefault(order.SupplierId, string.Empty);
}
