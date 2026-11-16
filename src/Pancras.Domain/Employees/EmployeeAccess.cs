namespace Pancras.Domain.Employees;

/// <summary>Người đang thao tác. Role và LocationId lấy từ phiên đăng nhập.</summary>
public sealed record EmployeeActor(Guid Id, string? Role, Guid? LocationId);

/// <summary>Bảng quyền về nhân viên. Khớp docs/roles-and-permissions.md mục 2.</summary>
public static class EmployeeAccess
{
    public const string ListRoles =
        EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant + "," + EmployeeRole.ShowroomWarehouseLead;

    /// <summary>Hiện mục menu Nhân sự (theo vai; phạm vi kho của Trưởng Showroom/Kho xử lý ở handler).</summary>
    public static bool CanList(string? role) =>
        role is EmployeeRole.Admin or EmployeeRole.Director or EmployeeRole.Accountant or EmployeeRole.ShowroomWarehouseLead;

    public static bool CanList(EmployeeActor actor) =>
        actor.Role is EmployeeRole.Admin or EmployeeRole.Director or EmployeeRole.Accountant
        || IsLead(actor);

    /// <summary>Null nghĩa là thấy mọi kho.</summary>
    public static Guid? ListScope(EmployeeActor actor) => IsLead(actor) ? actor.LocationId : null;

    public static bool CanView(EmployeeActor actor, Guid targetId, Guid? targetLocationId)
    {
        if (actor.Id == targetId)
        {
            return true;
        }

        if (!CanList(actor))
        {
            return false;
        }

        return !IsLead(actor) || (actor.LocationId is Guid own && own == targetLocationId);
    }

    public static bool CanCreate(EmployeeActor actor, string? newRole, Guid? newLocationId)
    {
        if (!EmployeeRole.IsAllowed(newRole))
        {
            return false;
        }

        return actor.Role switch
        {
            EmployeeRole.Admin => true,
            EmployeeRole.Director => newRole != EmployeeRole.Admin,
            EmployeeRole.ShowroomWarehouseLead =>
                newRole is EmployeeRole.WarehouseKeeper or EmployeeRole.Sales
                && actor.LocationId is Guid own
                && own == newLocationId,
            _ => false
        };
    }

    public static bool CanCreateAny(EmployeeActor actor) =>
        actor.Role is EmployeeRole.Admin or EmployeeRole.Director
        || (IsLead(actor) && actor.LocationId is not null);

    public static IReadOnlyList<string> CreatableRoles(EmployeeActor actor) =>
        actor.Role switch
        {
            EmployeeRole.Admin => EmployeeRole.All,
            EmployeeRole.Director => EmployeeRole.All.Where(role => role != EmployeeRole.Admin).ToArray(),
            EmployeeRole.ShowroomWarehouseLead => [EmployeeRole.WarehouseKeeper, EmployeeRole.Sales],
            _ => []
        };

    /// <summary>Họ tên, email, điện thoại, địa chỉ: Admin hoặc chính chủ.</summary>
    public static bool CanEditPersonal(EmployeeActor actor, Guid targetId) =>
        actor.Role == EmployeeRole.Admin || actor.Id == targetId;

    /// <summary>Vai trò, chức vụ, phòng ban, kho/showroom: Admin và Giám đốc, không tự sửa mình, Giám đốc không sửa Admin.</summary>
    public static bool CanEditAssignment(EmployeeActor actor, Guid targetId, string? targetRole)
    {
        if (actor.Id == targetId)
        {
            return false;
        }

        return actor.Role switch
        {
            EmployeeRole.Admin => true,
            EmployeeRole.Director => targetRole != EmployeeRole.Admin,
            _ => false
        };
    }

    public static bool CanAssignRole(EmployeeActor actor, string? newRole) =>
        EmployeeRole.IsAllowed(newRole)
        && (actor.Role == EmployeeRole.Admin
            || (actor.Role == EmployeeRole.Director && newRole != EmployeeRole.Admin));

    public static bool CanResetPassword(EmployeeActor actor) => actor.Role == EmployeeRole.Admin;

    public static bool CanSetActive(EmployeeActor actor, Guid targetId, string? targetRole) =>
        CanEditAssignment(actor, targetId, targetRole);

    private static bool IsLead(EmployeeActor actor) => actor.Role == EmployeeRole.ShowroomWarehouseLead;
}

public static class SupplierAccess
{
    /// <summary>Quản lý: thêm, sửa, ngừng hợp tác; xem tài khoản ngân hàng.</summary>
    public const string Roles =
        EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant;

    /// <summary>Xem danh bạ. Thủ kho và Trưởng Showroom/Kho chỉ xem, không thấy tài khoản ngân hàng.</summary>
    public const string ViewRoles =
        Roles + "," + EmployeeRole.WarehouseKeeper + "," + EmployeeRole.ShowroomWarehouseLead;

    public static bool CanView(string? role) =>
        CanManage(role) || role is EmployeeRole.WarehouseKeeper or EmployeeRole.ShowroomWarehouseLead;

    public static bool CanManage(string? role) =>
        role is EmployeeRole.Admin or EmployeeRole.Director or EmployeeRole.Accountant;

    /// <summary>Chỉ Admin khôi phục nhà cung cấp đã ngừng hợp tác.</summary>
    public static bool CanReactivate(string? role) => role is EmployeeRole.Admin;

    public static bool CanViewBank(string? role) => CanManage(role);
}

public static class LocationAccess
{
    public const string ViewRoles =
        EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant;

    public static bool CanView(string? role) =>
        role is EmployeeRole.Admin or EmployeeRole.Director or EmployeeRole.Accountant;

    public static bool CanManage(string? role) =>
        role is EmployeeRole.Admin or EmployeeRole.Director;
}
