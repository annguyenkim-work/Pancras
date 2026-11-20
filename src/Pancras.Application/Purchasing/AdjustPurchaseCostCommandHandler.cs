using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>
/// Điều chỉnh chi phí sau khi đơn đã nhận đủ. Khoản điều chỉnh được chia cho các dòng đã nhận theo cách chia đã chọn;
/// phần ứng với hàng còn tồn cộng vào giá vốn bình quân, phần ứng với hàng đã xuất ghi là chênh lệch (không sửa ngược).
/// </summary>
public sealed class AdjustPurchaseCostCommandHandler(
    IPurchaseOrderRepository orders,
    IInventoryRepository inventory,
    ICatalogItemRepository catalog,
    GetCatalogMeasuresHandler measures,
    IUnitOfWork unitOfWork,
    ILogger<AdjustPurchaseCostCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid, PurchaseError>> HandleAsync(AdjustPurchaseCostCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        if (!PurchaseAccess.CanManage(command.Actor))
        {
            logger.LogWarning("Purchase cost adjustment forbidden for role {Role}.", command.Actor.Role);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
        }

        var amount = PurchaseMoney.RoundDong(command.Amount);
        if (decimal.Abs(amount) > PurchaseMoney.MaxDong)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AmountTooLarge);
        }

        var received = order.Lines.Where(line => line.ReceivedQuantity > 0).ToArray();
        if (order.Status != PurchaseStatus.Received || amount == 0 || received.Length == 0)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AdjustmentInvalid);
        }

        var basis = await ChargeBasis.ComputeAsync(order, command.Method, null, measures, cancellationToken);
        if (basis is null || command.Method == ChargeSplitMethod.Manual)
        {
            return Result<Guid, PurchaseError>.Fail(command.Method == ChargeSplitMethod.Manual ? PurchaseError.AdjustmentInvalid : PurchaseError.MissingMeasure);
        }

        // Chia trị tuyệt đối rồi trả lại dấu: cách chia (cắt phần lẻ, dư về dòng cuối) chỉ định nghĩa cho số không âm.
        if (!ChargeAllocation.TrySplit(Math.Abs(amount), received.Select(line => basis[line.Id]).ToArray(), out var shares))
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.MissingMeasure);
        }

        var sign = Math.Sign(amount);
        var items = new Dictionary<Guid, CatalogItem>();
        var onHand = new Dictionary<Guid, int>();
        var uncovered = new Dictionary<Guid, int>();
        decimal applied = 0;
        for (var index = 0; index < received.Length; index++)
        {
            var line = received[index];
            if (!items.TryGetValue(line.CatalogItemId, out var item))
            {
                item = await catalog.GetVisibleForUpdateAsync(line.CatalogItemId, cancellationToken);
                if (item is null)
                {
                    return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
                }

                items[line.CatalogItemId] = item;
                onHand[line.CatalogItemId] = await inventory.SumOnHandAsync(line.CatalogItemId, cancellationToken);
                uncovered[line.CatalogItemId] = onHand[line.CatalogItemId];
            }

            // Số món của dòng này còn nằm trong kho (không vượt quá tồn chưa được dòng khác của cùng mặt hàng tính).
            var stillHeld = Math.Min(uncovered[line.CatalogItemId], line.ReceivedQuantity);
            uncovered[line.CatalogItemId] -= stillHeld;
            var portion = PurchaseMoney.RoundDong(shares[index] * stillHeld / line.ReceivedQuantity) * sign;
            if (portion != 0)
            {
                item.SetCostPrice(PurchaseCostSpread.Apply(onHand[line.CatalogItemId], item.CostPrice, portion));
                applied += portion;
            }
        }

        var at = (clock ?? TimeProvider.System).GetUtcNow();
        var adjustment = order.TryAddCostAdjustment(command.Label, amount, command.Method, command.Reason, applied, at, request?.ActorCode);
        if (adjustment is null)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AdjustmentInvalid);
        }

        order.AdvanceVersion();
        audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.PurchaseCostAdjusted, "purchase_order", order.Id.ToString("D"), adjustment.Label);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Purchase cost adjustment commit failed for {PurchaseOrderId}.", order.Id);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.SaveFailed);
        }

        logger.LogInformation("Adjusted cost of purchase order {PurchaseOrderId}.", order.Id);
        return Result<Guid, PurchaseError>.Ok(adjustment.Id);
    }

}
