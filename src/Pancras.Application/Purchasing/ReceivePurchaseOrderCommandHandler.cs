using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class ReceivePurchaseOrderCommandHandler(
    IPurchaseOrderRepository orders,
    IInventoryRepository inventory,
    ICatalogItemRepository catalog,
    ILogger<ReceivePurchaseOrderCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ReceivePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Receipt rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if (order.Status != PurchaseStatus.Open)
        {
            logger.LogWarning("Purchase order {PurchaseOrderId} is already received.", order.Id);
            return Result<Guid>.Fail(PurchaseError.AlreadyReceived);
        }

        if (order.Lines.Any(line => line.FobUsd <= 0))
        {
            // Giá FOB 0 sẽ được bình quân vào giá vốn công ty và kéo giá xuống.
            logger.LogWarning("Purchase order {PurchaseOrderId} has a line without a price.", order.Id);
            return Result<Guid>.Fail(PurchaseError.ZeroPrice);
        }

        if (order.HasUngroupedCharges)
        {
            logger.LogWarning("Purchase order {PurchaseOrderId} still has charges without a group.", order.Id);
            return Result<Guid>.Fail(PurchaseError.UngroupedCharge);
        }

        var items = new Dictionary<Guid, CatalogItem>();
        foreach (var line in order.Lines)
        {
            if (items.ContainsKey(line.CatalogItemId))
            {
                continue;
            }

            var item = await catalog.GetVisibleForUpdateAsync(line.CatalogItemId, cancellationToken);
            if (item is null)
            {
                logger.LogWarning("Receipt rejected for catalog item {CatalogItemId}.", line.CatalogItemId);
                return Result<Guid>.Fail(PurchaseError.InvalidOrder);
            }

            items[line.CatalogItemId] = item;
        }

        var balances = new Dictionary<Guid, StockBalance>();
        var previous = new Dictionary<Guid, (int OnHand, int Reserved, int InTransit)>();
        var previousCosts = items.ToDictionary(pair => pair.Key, pair => pair.Value.CostPrice);
        foreach (var itemId in items.Keys)
        {
            var balance = await inventory.FindBalanceForUpdateAsync(itemId, order.ReceiveLocationId, cancellationToken);
            if (balance is null)
            {
                balance = StockBalance.Create(itemId, order.ReceiveLocationId, 0, 0, 0);
                await inventory.AddBalanceAsync(balance, cancellationToken);
            }

            balances[itemId] = balance;
            previous[itemId] = (balance.OnHand, balance.Reserved, balance.InTransit);
        }

        var exchangeRate = order.ExchangeRate;
        if (!order.TryReceive((clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode))
        {
            return Result<Guid>.Fail(PurchaseError.AlreadyReceived);
        }

        var runningOnHand = new Dictionary<Guid, int>();
        var runningCost = new Dictionary<Guid, decimal>();
        foreach (var itemId in items.Keys)
        {
            runningOnHand[itemId] = await inventory.SumOnHandAsync(itemId, cancellationToken);
            runningCost[itemId] = previousCosts[itemId];
        }

        foreach (var line in order.Lines)
        {
            var balance = balances[line.CatalogItemId];
            var beforeTransit = balance.InTransit;
            if (!balance.TryAddOnHand(line.Quantity) || balance.InTransit != beforeTransit)
            {
                Restore(order, balances, previous, items, previousCosts);
                logger.LogWarning("Receipt rejected for purchase order {PurchaseOrderId}.", order.Id);
                return Result<Guid>.Fail(PurchaseError.InvalidOrder);
            }

            var unit = PurchaseMoney.RoundDong(PurchaseCosting.For(order, line).UnitCost);
            var next = LandedCostAverage.Next(runningOnHand[line.CatalogItemId], runningCost[line.CatalogItemId], line.Quantity, unit);
            runningOnHand[line.CatalogItemId] += line.Quantity;
            runningCost[line.CatalogItemId] = next;
            items[line.CatalogItemId].SetCostPrice(next);
            await inventory.AddMovementAsync(
                StockMovement.Create(
                    line.CatalogItemId,
                    order.ReceiveLocationId,
                    StockMovementKind.Receipt,
                    line.Quantity,
                    order.Id,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }

        if (order.ExchangeRate != exchangeRate || !await inventory.CommitAsync(cancellationToken))
        {
            Restore(order, balances, previous, items, previousCosts);
            logger.LogWarning("Receipt commit failed for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.SaveFailed);
        }

        logger.LogInformation("Received purchase order {PurchaseOrderId}.", order.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.PurchaseReceived, "purchase_order", order.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(order.Id);
    }

    private static void Restore(
        PurchaseOrder order,
        Dictionary<Guid, StockBalance> balances,
        Dictionary<Guid, (int OnHand, int Reserved, int InTransit)> previous,
        Dictionary<Guid, CatalogItem> items,
        Dictionary<Guid, decimal> previousCosts)
    {
        order.RestoreOpen();
        foreach (var pair in previous)
        {
            balances[pair.Key].RestoreUncommitted(pair.Value.OnHand, pair.Value.Reserved, pair.Value.InTransit);
        }

        foreach (var cost in previousCosts)
        {
            items[cost.Key].SetCostPrice(cost.Value);
        }
    }
}
