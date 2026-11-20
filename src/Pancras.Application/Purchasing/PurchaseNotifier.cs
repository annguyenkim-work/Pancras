using Pancras.Application.Employees;
using Pancras.Application.Notifications;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Notifications;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>
/// Thông báo các bước của đơn mua (xem docs notifications-and-audit.md mục A3).
/// Chỉ thêm vào đơn vị lưu của handler gọi; handler commit thì thông báo được lưu cùng đơn.
/// </summary>
public static class PurchaseNotifier
{
    public static async Task NotifyAsync(
        INoticeStore? notices,
        IEmployeeRepository? employees,
        PurchaseOrder order,
        PurchaseStatusAction action,
        string? note,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (notices is null || employees is null)
        {
            return;
        }

        var people = (await employees.ListReadOnlyAsync(cancellationToken)).Where(person => person.IsActive).ToArray();
        var createdBy = order.Events.FirstOrDefault(entry => entry.Kind == PurchaseEventKind.Created)?.ActorCode;
        bool IsCreator(Employee person) => !string.IsNullOrEmpty(createdBy)
            && string.Equals(person.Code, createdBy, StringComparison.OrdinalIgnoreCase);
        bool StaffOfReceiveLocation(Employee person) => person.StockLocationId == order.ReceiveLocationId
            && (InventoryRoles.IsShowroomLead(person.Role) || InventoryRoles.IsWarehouseKeeper(person.Role));

        (string Step, Func<Employee, bool> Matches, Guid? Location)? plan = action switch
        {
            PurchaseStatusAction.Submit => (Notification.PurchaseSubmittedStep, person => InventoryRoles.IsFinance(person.Role), null),
            PurchaseStatusAction.Approve => (Notification.PurchaseApprovedStep, person => IsCreator(person) || StaffOfReceiveLocation(person), order.ReceiveLocationId),
            PurchaseStatusAction.ReturnToDraft => (WithReason(Notification.PurchaseReturnedStep, note), IsCreator, null),
            PurchaseStatusAction.MarkInTransit => (Notification.PurchaseInTransitStep, StaffOfReceiveLocation, order.ReceiveLocationId),
            PurchaseStatusAction.Cancel => (WithReason(Notification.PurchaseCancelledStep, note), person => IsCreator(person) || StaffOfReceiveLocation(person), order.ReceiveLocationId),
            _ => null
        };

        // Thông báo cũ của đơn đã lỗi thời (ví dụ "chờ duyệt" sau khi đã duyệt) nên luôn thay.
        await notices.RemoveForDocumentAsync(order.Id, cancellationToken);
        if (plan is not { } chosen)
        {
            return;
        }

        foreach (var person in people.Where(person => person.Id != actorId && chosen.Matches(person)))
        {
            await notices.AddAsync(
                Notification.Create(person.Id, chosen.Location, order.Id, NotificationKind.Purchase, chosen.Step, at),
                cancellationToken);
        }
    }

    /// <summary>Đơn đã nhập kho (một phần hoặc đủ): báo người lập đơn và tài chính, trừ người nhận hàng.</summary>
    public static async Task NotifyReceivedAsync(
        INoticeStore? notices,
        IEmployeeRepository? employees,
        PurchaseOrder order,
        bool final,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (notices is null || employees is null)
        {
            return;
        }

        var createdBy = order.Events.FirstOrDefault(entry => entry.Kind == PurchaseEventKind.Created)?.ActorCode;
        var people = (await employees.ListReadOnlyAsync(cancellationToken)).Where(person => person.IsActive).ToArray();
        await notices.RemoveForDocumentAsync(order.Id, cancellationToken);
        var step = final ? Notification.PurchaseReceivedStep : Notification.PurchasePartiallyReceivedStep;
        foreach (var person in people.Where(person => person.Id != actorId
            && (InventoryRoles.IsFinance(person.Role)
                || (!string.IsNullOrEmpty(createdBy) && string.Equals(person.Code, createdBy, StringComparison.OrdinalIgnoreCase)))))
        {
            await notices.AddAsync(Notification.Create(person.Id, order.ReceiveLocationId, order.Id, NotificationKind.Purchase, step, at), cancellationToken);
        }
    }

    private static string WithReason(string step, string? note)
    {
        var reason = note?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return step;
        }

        var text = $"{step}: {reason}";
        return text.Length <= 120 ? text : text[..119] + "…";
    }
}
