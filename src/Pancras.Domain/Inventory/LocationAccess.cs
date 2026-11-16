using Pancras.Domain.Employees;

namespace Pancras.Domain.Inventory;

public static class LocationAccess
{
    public const string ViewRoles =
        EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant;

    public static bool CanView(string? role) =>
        role is EmployeeRole.Admin or EmployeeRole.Director or EmployeeRole.Accountant;

    public static bool CanManage(string? role) =>
        role is EmployeeRole.Admin or EmployeeRole.Director;
}
