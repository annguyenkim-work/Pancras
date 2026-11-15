using Pancras.Domain.Employees;

namespace Pancras.Domain.Catalog;

public static class PriceListRole
{
    public static bool CanEdit(string? role) =>
        role == EmployeeRole.Admin
        || role == EmployeeRole.Director
        || role == EmployeeRole.Accountant
        || role == EmployeeRole.ShowroomWarehouseLead;
}
