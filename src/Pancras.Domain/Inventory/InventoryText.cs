namespace Pancras.Domain.Inventory;

internal static class InventoryText
{
    public static bool TryOptional(string? value, int maxLength, out string stored)
    {
        stored = value?.Trim() ?? string.Empty;
        return stored.Length <= maxLength;
    }

    public static bool TryRequired(string? value, int maxLength, out string stored)
    {
        stored = value?.Trim() ?? string.Empty;
        return stored.Length >= 1 && stored.Length <= maxLength;
    }
}

public static class InventoryRoles
{
    public static bool IsWarehouseKeeper(string? role) =>
        string.Equals(role, Employees.EmployeeRole.WarehouseKeeper, StringComparison.Ordinal);

    public static bool IsShowroomLead(string? role) =>
        string.Equals(role, Employees.EmployeeRole.ShowroomWarehouseLead, StringComparison.Ordinal);

    public static bool IsFinance(string? role) =>
        string.Equals(role, Employees.EmployeeRole.Admin, StringComparison.Ordinal)
        || string.Equals(role, Employees.EmployeeRole.Director, StringComparison.Ordinal)
        || string.Equals(role, Employees.EmployeeRole.Accountant, StringComparison.Ordinal);

    public static bool CanDraftAdjustment(string? role) => IsWarehouseKeeper(role) || IsShowroomLead(role);

    public static bool CanCountStock(string? role) => IsWarehouseKeeper(role) || IsShowroomLead(role) || IsFinance(role);

    public static bool CanPostAdjustment(string? role) => IsFinance(role);
}
