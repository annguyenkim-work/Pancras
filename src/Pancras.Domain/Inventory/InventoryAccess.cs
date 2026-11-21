namespace Pancras.Domain.Inventory;

/// <summary>Bảng quyền kho. Khớp docs/roles-and-permissions.md mục 3.</summary>
public static class InventoryAccess
{
    public const string ViewRoles =
        Employees.EmployeeRole.Admin + "," + Employees.EmployeeRole.Director + "," + Employees.EmployeeRole.Accountant + ","
        + Employees.EmployeeRole.ShowroomWarehouseLead + "," + Employees.EmployeeRole.WarehouseKeeper + "," + Employees.EmployeeRole.Sales;

    public static bool CanView(string? role) =>
        InventoryRoles.IsFinance(role)
        || InventoryRoles.IsShowroomLead(role)
        || InventoryRoles.IsWarehouseKeeper(role)
        || string.Equals(role, Employees.EmployeeRole.Sales, StringComparison.Ordinal);

    /// <summary>Admin, Giám đốc, Kế toán thấy mọi kho và ghi thẳng, không qua duyệt.</summary>
    public static bool IsGlobal(string? role) => InventoryRoles.IsFinance(role);

    /// <summary>Null là mọi kho. Guid.Empty là không thấy kho nào (chưa được gán kho).</summary>
    public static Guid? Scope(Employees.EmployeeActor actor) =>
        IsGlobal(actor.Role) ? null : actor.LocationId ?? Guid.Empty;

    public static bool CanAccessLocation(Employees.EmployeeActor actor, Guid locationId) =>
        IsGlobal(actor.Role) || (actor.LocationId is Guid own && own != Guid.Empty && own == locationId);

    public static bool CanSeeCost(string? role) => InventoryRoles.IsFinance(role);

    public static bool CanExport(string? role) => InventoryRoles.IsFinance(role) || InventoryRoles.IsShowroomLead(role);

    /// <summary>Lập phiếu chuyển, kiểm kê, điều chỉnh, hủy.</summary>
    public static bool CanOperate(string? role) =>
        InventoryRoles.IsFinance(role) || InventoryRoles.IsShowroomLead(role) || InventoryRoles.IsWarehouseKeeper(role);

    /// <summary>Duyệt kiểm kê và phiếu hủy: tài chính duyệt mọi phiếu; Trưởng kho chỉ duyệt phiếu của Thủ kho trong kho mình.</summary>
    public static bool CanApprove(Employees.EmployeeActor actor, string? creatorRole, Guid documentLocationId)
    {
        if (InventoryRoles.IsFinance(actor.Role))
        {
            return true;
        }

        return InventoryRoles.IsShowroomLead(actor.Role)
            && actor.LocationId is Guid own
            && own == documentLocationId
            && (string.IsNullOrEmpty(creatorRole) || InventoryRoles.IsWarehouseKeeper(creatorRole));
    }

    public static bool CanApproveAdjustment(string? role) => InventoryRoles.IsFinance(role);
}
