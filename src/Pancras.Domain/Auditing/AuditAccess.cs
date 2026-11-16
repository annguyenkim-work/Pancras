using Pancras.Domain.Employees;

namespace Pancras.Domain.Auditing;

public static class AuditAccess
{
    public const string ViewRoles = EmployeeRole.Admin + "," + EmployeeRole.Director;

    public static bool CanView(string? role) => role is EmployeeRole.Admin or EmployeeRole.Director;
}
