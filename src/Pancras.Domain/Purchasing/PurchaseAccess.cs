using Pancras.Domain.Employees;

namespace Pancras.Domain.Purchasing;

/// <summary>Bảng quyền module Mua hàng. Một nơi duy nhất để UI và handler cùng dùng.</summary>
public static class PurchaseAccess
{
    public const string ManageRoles = EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant;

    /// <summary>Vào được trang: vai trò quản lý, và kho (chỉ thấy đơn về kho mình, không thấy giá).</summary>
    public const string ViewRoles = ManageRoles + "," + EmployeeRole.WarehouseKeeper + "," + EmployeeRole.ShowroomWarehouseLead;

    /// <summary>Mục "Mua hàng" ở thanh bên và quyền vào trang, theo cùng danh sách với <see cref="ViewRoles"/>.</summary>
    public static bool CanOpenPage(string? role) =>
        role is not null && ViewRoles.Split(',').Contains(role, StringComparer.Ordinal);

    /// <summary>Lập, sửa, chuyển trạng thái, hủy, điều chỉnh chi phí, quản lý chứng từ và xem giá vốn.</summary>
    public static bool CanManage(EmployeeActor? actor) =>
        actor is not null && actor.Id != Guid.Empty && actor.Role is EmployeeRole.Admin or EmployeeRole.Director or EmployeeRole.Accountant;

    /// <summary>Chỉ các vai trò quản lý mới thấy giá FOB, thuế và giá vốn (file xuất cũng vậy).</summary>
    public static bool CanViewCost(EmployeeActor? actor) => CanManage(actor);

    /// <summary>Thủ kho, Trưởng Showroom/Kho: chỉ làm việc với kho mình được gán.</summary>
    public static bool IsWarehouseStaff(EmployeeActor? actor) =>
        actor is not null
        && actor.Id != Guid.Empty
        && actor.LocationId is not null
        && actor.Role is EmployeeRole.WarehouseKeeper or EmployeeRole.ShowroomWarehouseLead;

    /// <summary>Kho chỉ thấy đơn đã duyệt trở đi của kho mình; đơn nháp, chờ duyệt, đã hủy không hiện.</summary>
    public static bool CanView(EmployeeActor? actor, Guid receiveLocationId, PurchaseStatus status) =>
        CanManage(actor)
        || (IsWarehouseStaff(actor)
            && actor!.LocationId == receiveLocationId
            && status is PurchaseStatus.Approved or PurchaseStatus.InTransit or PurchaseStatus.PartiallyReceived or PurchaseStatus.Received);

    /// <summary>Nhận hàng: vai trò quản lý, hoặc kho đúng kho nhận của đơn.</summary>
    public static bool CanReceive(EmployeeActor? actor, Guid receiveLocationId) =>
        CanManage(actor) || (IsWarehouseStaff(actor) && actor!.LocationId == receiveLocationId);
}
