using System.Linq.Expressions;
using Pancras.Domain.Employees;

namespace Pancras.Domain.Inventory;

/// <summary>Nhóm phiếu chuyển "đang chờ tôi": duyệt, xuất kho, nhận hàng, xử lý chênh lệch, hoặc bất kỳ nhóm nào.</summary>
public enum TransferWaitingKind
{
    Decide,
    Ship,
    Receive,
    Resolve,
    Any
}

/// <summary>
/// Quyền xem và bước chờ xử lý của phiếu chuyển: mỗi quy tắc có dạng hàm (cho giao diện, từng phiếu) và dạng điều kiện
/// (<see cref="Visible"/>, <see cref="Waiting"/>) để database lọc, cắt trang và đếm. Hai dạng được test đối chiếu từng ô.
/// </summary>
public static class TransferWorkflowAccess
{
    /// <summary>Người duyệt: Admin, Giám đốc, Kế toán, hoặc Trưởng kho của chính kho xuất.</summary>
    public static bool CanDecide(EmployeeActor actor, Guid sourceLocationId) =>
        InventoryRoles.IsFinance(actor.Role)
        || (InventoryRoles.IsShowroomLead(actor.Role) && actor.LocationId is Guid own && own == sourceLocationId);

    public static bool IsWaiting(EmployeeActor actor, TransferWaitingKind kind, TransferStatus status, Guid sourceLocationId, Guid destinationLocationId) =>
        kind switch
        {
            TransferWaitingKind.Decide => status == TransferStatus.Requested && CanDecide(actor, sourceLocationId),
            TransferWaitingKind.Ship => status == TransferStatus.Approved && AtLocation(actor, sourceLocationId),
            TransferWaitingKind.Receive => status == TransferStatus.InTransit && AtLocation(actor, destinationLocationId),
            TransferWaitingKind.Resolve => status == TransferStatus.ReceivedWithDiscrepancy && InventoryRoles.IsFinance(actor.Role),
            _ => IsWaiting(actor, TransferWaitingKind.Decide, status, sourceLocationId, destinationLocationId)
                || IsWaiting(actor, TransferWaitingKind.Ship, status, sourceLocationId, destinationLocationId)
                || IsWaiting(actor, TransferWaitingKind.Receive, status, sourceLocationId, destinationLocationId)
                || IsWaiting(actor, TransferWaitingKind.Resolve, status, sourceLocationId, destinationLocationId)
        };

    /// <summary>Phiếu người dùng được thấy: có quyền thao tác kho và là kho xuất hoặc kho nhận của mình (tài chính thấy mọi phiếu).</summary>
    public static Expression<Func<TransferSlip, bool>> Visible(EmployeeActor actor)
    {
        if (!InventoryAccess.CanOperate(actor.Role))
        {
            return slip => false;
        }

        if (InventoryAccess.IsGlobal(actor.Role))
        {
            return slip => true;
        }

        if (OwnLocation(actor) is not Guid own)
        {
            return slip => false;
        }

        return slip => slip.SourceLocationId == own || slip.DestinationLocationId == own;
    }

    /// <summary>Điều kiện cùng nghĩa với <see cref="IsWaiting"/>; chưa gồm quyền xem (kết hợp với <see cref="Visible"/>).</summary>
    public static Expression<Func<TransferSlip, bool>> Waiting(EmployeeActor actor, TransferWaitingKind kind)
    {
        var finance = InventoryRoles.IsFinance(actor.Role);
        var lead = InventoryRoles.IsShowroomLead(actor.Role) && actor.LocationId is Guid;
        var leadLocation = actor.LocationId ?? Guid.Empty;
        var operate = InventoryAccess.CanOperate(actor.Role);
        var global = InventoryAccess.IsGlobal(actor.Role);
        var own = OwnLocation(actor) ?? Guid.Empty;
        var hasOwn = OwnLocation(actor) is not null;

        Expression<Func<TransferSlip, bool>> decide = slip =>
            slip.Status == TransferStatus.Requested
            && (finance || (lead && slip.SourceLocationId == leadLocation));
        Expression<Func<TransferSlip, bool>> ship = slip =>
            slip.Status == TransferStatus.Approved
            && operate
            && (global || (hasOwn && slip.SourceLocationId == own));
        Expression<Func<TransferSlip, bool>> receive = slip =>
            slip.Status == TransferStatus.InTransit
            && operate
            && (global || (hasOwn && slip.DestinationLocationId == own));
        Expression<Func<TransferSlip, bool>> resolve = slip =>
            slip.Status == TransferStatus.ReceivedWithDiscrepancy && finance;

        return kind switch
        {
            TransferWaitingKind.Decide => decide,
            TransferWaitingKind.Ship => ship,
            TransferWaitingKind.Receive => receive,
            TransferWaitingKind.Resolve => resolve,
            _ => slip =>
                (slip.Status == TransferStatus.Requested && (finance || (lead && slip.SourceLocationId == leadLocation)))
                || (slip.Status == TransferStatus.Approved && operate && (global || (hasOwn && slip.SourceLocationId == own)))
                || (slip.Status == TransferStatus.InTransit && operate && (global || (hasOwn && slip.DestinationLocationId == own)))
                || (slip.Status == TransferStatus.ReceivedWithDiscrepancy && finance)
        };
    }

    private static bool AtLocation(EmployeeActor actor, Guid locationId) =>
        InventoryAccess.CanOperate(actor.Role) && InventoryAccess.CanAccessLocation(actor, locationId);

    private static Guid? OwnLocation(EmployeeActor actor) =>
        actor.LocationId is Guid own && own != Guid.Empty ? own : null;
}
