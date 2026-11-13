using Microsoft.Extensions.Logging;
using Pancras.Application.Inventory;
using Pancras.Application.Suppliers;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class ListPurchaseOrdersHandler(
    IPurchaseOrderRepository orders,
    ISupplierRepository suppliers,
    IInventoryRepository inventory,
    ILogger<ListPurchaseOrdersHandler> logger)
{
    public async Task<IReadOnlyList<PurchaseOrderSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        var list = await orders.ListAsync(cancellationToken);
        var supplierNames = (await suppliers.ListAsync(cancellationToken)).ToDictionary(supplier => supplier.Id, supplier => supplier.Name);
        var locations = (await inventory.ListLocationsAsync(cancellationToken)).ToDictionary(location => location.Id, location => location.Name);
        logger.LogInformation("Listed {PurchaseOrderCount} purchase orders.", list.Count);
        return list
            .OrderByDescending(order => order.Id)
            .Select(order => new PurchaseOrderSummary(
                order.Id,
                order.Code,
                order.Lines.Count,
                supplierNames.GetValueOrDefault(order.SupplierId, string.Empty),
                order.ContainerNumber,
                order.BillOfLading,
                order.Port,
                order.ExpectedOn,
                locations.GetValueOrDefault(order.ReceiveLocationId, string.Empty),
                order.Status,
                order.Lines.Sum(line => PurchaseCosting.For(order, line).LandedCost)))
            .ToArray();
    }
}

public sealed class GetPurchaseOrderHandler(
    IPurchaseOrderRepository orders,
    ISupplierRepository suppliers,
    IInventoryRepository inventory,
    ILogger<GetPurchaseOrderHandler> logger)
{
    public async Task<PurchaseOrderDetail?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var order = await orders.FindReadOnlyAsync(id, cancellationToken);
        if (order is null)
        {
            logger.LogInformation("Purchase order {PurchaseOrderId} was not found.", id);
            return null;
        }

        var supplier = await suppliers.GetActiveReadOnlyAsync(order.SupplierId, cancellationToken);
        var locations = await inventory.ListLocationsAsync(cancellationToken);
        var items = await inventory.ListItemChoicesAsync(cancellationToken);
        var itemMap = items.ToDictionary(item => item.Id);
        var locationName = locations.FirstOrDefault(location => location.Id == order.ReceiveLocationId)?.Name ?? string.Empty;
        logger.LogInformation("Read purchase order {PurchaseOrderId}.", order.Id);
        return new PurchaseOrderDetail(
            order.Id,
            order.Code,
            supplier?.Name ?? string.Empty,
            order.ContainerNumber,
            order.BillOfLading,
            order.Port,
            order.ExpectedOn,
            locationName,
            order.ReceiveLocationId,
            order.ExchangeRate,
            order.VatRate,
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
                    line.FobUsd,
                    line.DutyRate,
                    line.SupplementalDuty,
                    line.SpecialConsumptionTax,
                    line.EnvironmentTax,
                    line.DeclaredCustomsValue,
                    line.DeclaredDuty,
                    line.DeclaredVat,
                    PurchaseCosting.For(order, line));
            }).ToArray(),
            order.Charges.Select(charge => new PurchaseChargeView(
                charge.Id,
                charge.Amount,
                charge.Method,
                charge.Group,
                charge.Shares.Select(share => new PurchaseShareView(share.LineId, share.Amount)).ToArray())).ToArray());
    }
}
