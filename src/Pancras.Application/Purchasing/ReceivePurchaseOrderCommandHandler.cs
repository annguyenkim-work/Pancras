using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>
/// Nhận hàng của một đơn đã duyệt theo số thực nhận từng dòng: ghi tồn, ghi phiếu nhập và cập nhật giá vốn bình quân.
/// Có thể nhận nhiều lần; lần cuối đóng đơn và dồn phần giá vốn còn lại vào số thực nhận.
/// </summary>
public sealed class ReceivePurchaseOrderCommandHandler(
    IPurchaseOrderRepository orders,
    IInventoryRepository inventory,
    ICatalogItemRepository catalog,
    IUnitOfWork unitOfWork,
    ILogger<ReceivePurchaseOrderCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    Pancras.Application.Notifications.INoticeStore? notices = null,
    Pancras.Application.Employees.IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid, PurchaseError>> HandleAsync(ReceivePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            // Không lộ đơn có tồn tại hay không cho người không có quyền.
            logger.LogInformation("Receipt rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid, PurchaseError>.Fail(PurchaseAccess.CanManage(command.Actor) ? PurchaseError.InvalidOrder : PurchaseError.Forbidden);
        }

        if (!PurchaseAccess.CanReceive(command.Actor, order.ReceiveLocationId))
        {
            logger.LogWarning("Purchase receipt forbidden for role {Role}.", command.Actor.Role);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        if (order.Status == PurchaseStatus.Received)
        {
            logger.LogInformation("Purchase order {PurchaseOrderId} is already received.", order.Id);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AlreadyReceived);
        }

        if (!order.IsReceivable)
        {
            logger.LogInformation("Purchase order {PurchaseOrderId} is not receivable in status {Status}.", order.Id, order.Status);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.NotReceivable);
        }

        if (order.Lines.Any(line => line.FobUsd <= 0))
        {
            // Giá FOB 0 sẽ được bình quân vào giá vốn công ty và kéo giá xuống.
            logger.LogInformation("Purchase order {PurchaseOrderId} has a line without a price.", order.Id);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.ZeroPrice);
        }

        if (order.HasUngroupedCharges)
        {
            logger.LogInformation("Purchase order {PurchaseOrderId} still has charges without a group.", order.Id);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.UngroupedCharge);
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
                logger.LogInformation("Receipt rejected for catalog item {CatalogItemId}.", line.CatalogItemId);
                return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
            }

            items[line.CatalogItemId] = item;
        }

        var balances = new Dictionary<Guid, StockBalance>();
        var runningOnHand = new Dictionary<Guid, int>();
        var runningCost = new Dictionary<Guid, decimal>();
        foreach (var itemId in items.Keys)
        {
            var balance = await inventory.FindBalanceForUpdateAsync(itemId, order.ReceiveLocationId, cancellationToken);
            if (balance is null)
            {
                balance = StockBalance.Create(itemId, order.ReceiveLocationId, 0, 0, 0);
                await inventory.AddBalanceAsync(balance, cancellationToken);
            }

            balances[itemId] = balance;
            runningOnHand[itemId] = await inventory.SumOnHandAsync(itemId, cancellationToken);
            runningCost[itemId] = items[itemId].CostPrice;
        }

        var actuals = command.Actuals
            ?? order.Lines.ToDictionary(line => line.Id, line => Math.Max(0, line.Quantity - line.ReceivedQuantity));
        var exchangeRate = order.ExchangeRate;
        var receivedAt = (clock ?? TimeProvider.System).GetUtcNow();
        var decision = order.TryRecordReceipt(actuals, command.Final, command.Note, receivedAt, request?.ActorCode, out var results);
        if (decision != ReceiptDecision.Ok)
        {
            logger.LogInformation("Receipt rejected for purchase order {PurchaseOrderId}: {Decision}.", order.Id, decision);
            return Result<Guid, PurchaseError>.Fail(decision switch
            {
                ReceiptDecision.NoteRequired => PurchaseError.ReceiptNoteRequired,
                ReceiptDecision.InvalidQuantity => PurchaseError.InvalidReceiptQuantity,
                _ => PurchaseError.NotReceivable
            });
        }

        foreach (var result in results)
        {
            var itemId = result.Line.CatalogItemId;
            if (result.Quantity > 0)
            {
                var balance = balances[itemId];
                var beforeTransit = balance.InTransit;
                if (!balance.TryAddOnHand(result.Quantity) || balance.InTransit != beforeTransit)
                {
                    logger.LogInformation("Receipt rejected for purchase order {PurchaseOrderId}.", order.Id);
                    return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
                }

                var next = LandedCostAverage.Next(runningOnHand[itemId], runningCost[itemId], result.Quantity, result.UnitCost);
                runningOnHand[itemId] += result.Quantity;
                runningCost[itemId] = next;
                items[itemId].SetCostPrice(next);
                await inventory.AddMovementAsync(
                    StockMovement.Create(itemId, order.ReceiveLocationId, StockMovementKind.Receipt, result.Quantity, order.Id, receivedAt),
                    cancellationToken);
            }
            else if (result.Revaluation != 0 && runningOnHand[itemId] > 0)
            {
                // Đóng đơn mà dòng này không nhận thêm: phần giá vốn còn lại dồn vào hàng đang tồn.
                var next = PurchaseCostSpread.Apply(runningOnHand[itemId], runningCost[itemId], result.Revaluation);
                runningCost[itemId] = next;
                items[itemId].SetCostPrice(next);
            }
        }

        order.AdvanceVersion();
        await PurchaseNotifier.NotifyReceivedAsync(notices, employees, order, command.Final, command.Actor.Id, (clock ?? TimeProvider.System).GetUtcNow(), cancellationToken);
        if (order.ExchangeRate != exchangeRate)
        {
            logger.LogWarning("Receipt rejected for purchase order {PurchaseOrderId}: the exchange rate changed.", order.Id);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.SaveFailed);
        }

        audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.PurchaseReceived, "purchase_order", order.Id.ToString("D"), command.Final ? "final" : "partial");
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Receipt commit failed for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.SaveFailed);
        }

        logger.LogInformation("Received purchase order {PurchaseOrderId} (final: {Final}).", order.Id, command.Final);
        return Result<Guid, PurchaseError>.Ok(order.Id);
    }
}

/// <summary>Dồn một khoản tiền (có dấu) vào giá vốn bình quân của số hàng đang tồn.</summary>
internal static class PurchaseCostSpread
{
    public static decimal Apply(int onHand, decimal currentCost, decimal amount) =>
        onHand <= 0
            ? currentCost
            : Math.Max(0, PurchaseMoney.RoundDong((onHand * currentCost + amount) / onHand));
}
