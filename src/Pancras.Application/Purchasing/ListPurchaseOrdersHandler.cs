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
    /// <summary>
    /// Một trang đơn mua đã lọc và cắt trong database. Vai trò quản lý thấy mọi đơn kèm giá vốn. Kho chỉ thấy đơn đã duyệt trở đi
    /// của kho mình, không có số tiền (điều kiện xem lấy từ <see cref="PurchaseAccess.Visible"/>).
    /// </summary>
    public async Task<PurchaseOrderListPage> HandleAsync(EmployeeActor actor, PurchaseListFilter? filter, CancellationToken cancellationToken)
    {
        var costVisible = PurchaseAccess.CanViewCost(actor);
        var page = await orders.ListPageAsync(
            new PurchaseOrderQuery(PurchaseAccess.Visible(actor), filter?.SupplierId, filter?.Term?.Trim()),
            filter?.PageNumber ?? 1,
            filter?.PageSize ?? Pancras.Application.Common.PageSizes.Default,
            cancellationToken);
        // Ưu tiên tên đã chụp lúc lập đơn; đơn cũ chưa có bản chụp thì lấy tên hiện tại (kể cả nhà cung cấp đã ngừng hợp tác).
        var supplierNames = await suppliers.GetNamesAsync(page.Items.Select(order => order.SupplierId).Distinct().ToArray(), cancellationToken);
        var locations = (await inventory.ListLocationsAsync(cancellationToken)).ToDictionary(location => location.Id, location => location.Name);
        logger.LogInformation("Listed {PurchaseOrderCount} of {PurchaseOrderTotal} purchase orders.", page.Items.Count, page.Total);
        var rows = page.Items
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
        return new PurchaseOrderListPage(rows, page.Total, page.PageNumber, page.AnyVisible, page.FirstId);
    }
}

/// <summary>Bộ lọc và vị trí trang của danh sách đơn mua (cỡ trang 20 hoặc 50).</summary>
public sealed record PurchaseListFilter(Guid? SupplierId = null, string? Term = null, int PageNumber = 1, int PageSize = Pancras.Application.Common.PageSizes.Default);

/// <summary>Các dòng của một trang đơn mua, tổng số đơn khớp bộ lọc, số trang đã dùng, còn đơn nào xem được không và Id đơn đầu danh sách.</summary>
public sealed record PurchaseOrderListPage(IReadOnlyList<PurchaseOrderSummary> Items, int Total, int PageNumber, bool HasAny, Guid? FirstId);

internal static class PurchaseSupplierNames
{
    public static string Of(PurchaseOrder order, IReadOnlyDictionary<Guid, string> current) =>
        order.SupplierName.Length > 0 ? order.SupplierName : current.GetValueOrDefault(order.SupplierId, string.Empty);
}
