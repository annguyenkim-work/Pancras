using Pancras.Domain.Employees;

namespace Pancras.Domain.Catalog;

public static class CatalogAccess
{
    public const string ViewRoles =
        EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant;

    /// <summary>Disable / Active mặt hàng: cùng nhóm vai với xem danh mục.</summary>
    public static bool CanDisable(string? role) => CanView(role);

    public static bool CanView(string? role) =>
        role == EmployeeRole.Admin
        || role == EmployeeRole.Director
        || role == EmployeeRole.Accountant;
}
