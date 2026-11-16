using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>Chuyển trạng thái đơn mua: gửi duyệt, duyệt, trả về nháp, đánh dấu hàng đang về, hủy. Mỗi bước ghi vào lịch sử đơn.</summary>
public sealed class ChangePurchaseStatusCommandHandler(
    IPurchaseOrderRepository orders,
    ILogger<ChangePurchaseStatusCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    Pancras.Application.Notifications.INoticeStore? notices = null,
    Pancras.Application.Employees.IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid, PurchaseError>> HandleAsync(ChangePurchaseStatusCommand command, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(command.Actor))
        {
            logger.LogWarning("Purchase status change forbidden for role {Role}.", command.Actor.Role);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
        }

        if (command.Action is PurchaseStatusAction.Cancel or PurchaseStatusAction.ReturnToDraft)
        {
            var note = command.Note?.Trim() ?? string.Empty;
            if (note.Length == 0)
            {
                return Result<Guid, PurchaseError>.Fail(PurchaseError.ReasonRequired);
            }

            if (note.Length > PurchaseOrder.ReasonMaxLength)
            {
                return Result<Guid, PurchaseError>.Fail(PurchaseError.ReasonTooLong);
            }
        }

        var at = (clock ?? TimeProvider.System).GetUtcNow();
        var by = request?.ActorCode;
        var moved = command.Action switch
        {
            PurchaseStatusAction.Submit => order.TrySubmit(at, by),
            PurchaseStatusAction.Approve => order.TryApprove(at, by),
            PurchaseStatusAction.ReturnToDraft => order.TryReturnToDraft(command.Note, at, by),
            PurchaseStatusAction.MarkInTransit => order.TryMarkInTransit(at, by),
            PurchaseStatusAction.Cancel => order.TryCancel(command.Note, at, by),
            _ => false
        };
        if (!moved)
        {
            logger.LogWarning("Purchase order {PurchaseOrderId} cannot {Action} from {Status}.", order.Id, command.Action, order.Status);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidTransition);
        }

        order.StampUpdated(at, by);
        order.AdvanceVersion();
        await PurchaseNotifier.NotifyAsync(notices, employees, order, command.Action, command.Note, command.Actor.Id, at, cancellationToken);
        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Purchase status commit failed for {PurchaseOrderId}.", order.Id);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.SaveFailed);
        }

        logger.LogInformation("Purchase order {PurchaseOrderId} moved to {Status}.", order.Id, order.Status);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.PurchaseStatusChanged, "purchase_order", order.Id.ToString("D"), command.Action.ToString(), cancellationToken);
        return Result<Guid, PurchaseError>.Ok(order.Id);
    }
}
