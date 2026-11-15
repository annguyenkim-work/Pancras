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

public sealed class GetPurchaseOrderHandler(
    IPurchaseOrderRepository orders,
    ISupplierRepository suppliers,
    IInventoryRepository inventory,
    ILogger<GetPurchaseOrderHandler> logger)
{
    private static readonly PurchaseLineCost NoCost = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Trả về null nếu không có đơn hoặc người dùng không được xem. Người không được xem giá nhận bản không có giá, thuế, chi phí.</summary>
    public async Task<PurchaseOrderDetail?> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var order = await orders.FindReadOnlyAsync(id, cancellationToken);
        if (order is null || !PurchaseAccess.CanView(actor, order.ReceiveLocationId, order.Status))
        {
            logger.LogInformation("Purchase order {PurchaseOrderId} was not found or is not visible.", id);
            return null;
        }

        var costVisible = PurchaseAccess.CanViewCost(actor);
        var supplierNames = await suppliers.GetNamesAsync([order.SupplierId], cancellationToken);
        var locations = await inventory.ListLocationsAsync(cancellationToken);
        var items = await inventory.ListItemChoicesAsync(cancellationToken);
        var itemMap = items.ToDictionary(item => item.Id);
        var locationName = locations.FirstOrDefault(location => location.Id == order.ReceiveLocationId)?.Name ?? string.Empty;
        logger.LogInformation("Read purchase order {PurchaseOrderId}.", order.Id);
        return new PurchaseOrderDetail(
            order.Id,
            order.Code,
            PurchaseSupplierNames.Of(order, supplierNames),
            order.ContainerNumber,
            order.BillOfLading,
            order.Port,
            order.ExpectedOn,
            locationName,
            order.ReceiveLocationId,
            costVisible ? order.ExchangeRate : 0,
            costVisible ? order.VatRate : 0,
            order.Status,
            order.Lines.Select(line =>
            {
                itemMap.TryGetValue(line.CatalogItemId, out var item);
                return new PurchaseLineView(
                    line.Id,
                    line.CatalogItemId,
                    item?.Code ?? string.Empty,
                    item?.Name ?? string.Empty,
                    line.Quantity,
                    costVisible ? line.FobUsd : 0,
                    costVisible ? line.DutyRate : 0,
                    costVisible ? line.SupplementalDuty : 0,
                    costVisible ? line.SpecialConsumptionTax : 0,
                    costVisible ? line.EnvironmentTax : 0,
                    costVisible ? PurchaseCosting.For(order, line) : NoCost,
                    line.ReceivedQuantity);
            }).ToArray(),
            costVisible
                ? order.Charges.Select(charge => new PurchaseChargeView(
                    charge.Id,
                    charge.Amount,
                    charge.Method,
                    charge.Group,
                    charge.Shares.Select(share => new PurchaseShareView(share.LineId, share.Amount)).ToArray(),
                    charge.Label)).ToArray()
                : [],
            order.VatDeductible,
            order.SupplierId,
            order.UpdatedAt,
            order.UpdatedBy,
            order.ReceivedAt,
            order.ReceivedBy,
            costVisible ? order.ExchangeRateOn : null,
            costVisible ? order.ExchangeRateSource : string.Empty,
            costVisible,
            order.Events
                .OrderBy(item => item.OccurredAt)
                .Select(item => new PurchaseEventView(item.Kind, item.OccurredAt, item.ActorCode, item.Kind == PurchaseEventKind.CostAdjusted && !costVisible ? string.Empty : item.Note))
                .ToArray(),
            costVisible
                ? order.Adjustments
                    .OrderBy(item => item.CreatedAt)
                    .Select(item => new PurchaseAdjustmentView(item.Id, item.Label, item.Amount, item.Method, item.Reason, item.AppliedToStock, item.Variance, item.CreatedAt, item.CreatedBy))
                    .ToArray()
                : [],
            order.Version);
    }
}

/// <summary>Xem trước tác động lên giá vốn bình quân nếu nhận đủ phần còn lại của đơn (để người bấm nhận hàng thấy giá vốn cũ → mới).</summary>
public sealed class GetPurchaseReceiptPreviewHandler(
    IPurchaseOrderRepository orders,
    Pancras.Application.Catalog.ICatalogItemRepository catalog,
    IInventoryRepository inventory)
{
    public async Task<IReadOnlyList<PurchaseReceiptPreviewLine>> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanViewCost(actor))
        {
            return [];
        }

        var order = await orders.FindReadOnlyAsync(id, cancellationToken);
        if (order is null || !order.IsReceivable)
        {
            return [];
        }

        var onHand = new Dictionary<Guid, int>();
        var cost = new Dictionary<Guid, decimal>();
        var result = new List<PurchaseReceiptPreviewLine>(order.Lines.Count);
        foreach (var line in order.Lines)
        {
            if (!onHand.ContainsKey(line.CatalogItemId))
            {
                var item = await catalog.GetByIdReadOnlyAsync(line.CatalogItemId, cancellationToken);
                onHand[line.CatalogItemId] = await inventory.SumOnHandAsync(line.CatalogItemId, cancellationToken);
                cost[line.CatalogItemId] = item?.CostPrice ?? 0;
            }

            var remaining = Math.Max(0, line.Quantity - line.ReceivedQuantity);
            var before = cost[line.CatalogItemId];
            var held = onHand[line.CatalogItemId];
            var projected = before;
            if (remaining > 0)
            {
                var landed = PurchaseMoney.RoundDong(PurchaseCosting.For(order, line).LandedCost);
                var unit = PurchaseMoney.RoundDong(Math.Max(0, landed - line.CostedAmount) / remaining);
                projected = LandedCostAverage.Next(held, before, remaining, unit);
                onHand[line.CatalogItemId] = held + remaining;
                cost[line.CatalogItemId] = projected;
            }

            result.Add(new PurchaseReceiptPreviewLine(line.Id, remaining, held, before, projected));
        }

        return result;
    }
}

internal static class PurchaseSupplierNames
{
    public static string Of(PurchaseOrder order, IReadOnlyDictionary<Guid, string> current) =>
        order.SupplierName.Length > 0 ? order.SupplierName : current.GetValueOrDefault(order.SupplierId, string.Empty);
}
