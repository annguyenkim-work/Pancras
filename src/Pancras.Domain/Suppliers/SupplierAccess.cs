using Pancras.Domain.Employees;

namespace Pancras.Domain.Suppliers;

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
