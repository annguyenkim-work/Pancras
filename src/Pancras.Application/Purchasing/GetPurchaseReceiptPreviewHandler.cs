using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

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
