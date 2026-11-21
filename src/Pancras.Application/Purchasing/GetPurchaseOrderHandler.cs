using Microsoft.Extensions.Logging;
using Pancras.Application.Inventory;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

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
                    line.ReceivedQuantity,
                    costVisible ? line.CostedAmount : 0);
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
