using Pancras.Domain.Employees;

namespace Pancras.Domain.Catalog;

public static class CatalogAccess
{
    public const string ViewRoles =
        EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant;

    public static bool CanView(string? role) =>
        role == EmployeeRole.Admin
        || role == EmployeeRole.Director
        || role == EmployeeRole.Accountant;
}
