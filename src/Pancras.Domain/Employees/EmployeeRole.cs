namespace Pancras.Domain.Employees;

public static class EmployeeRole
{
    public const string Admin = "Admin";
    public const string Director = "Giám đốc";
    public const string Accountant = "Kế toán";
    public const string Sales = "Sales";
    public const string WarehouseKeeper = "Thủ kho";
    public const string ShowroomWarehouseLead = "Trưởng Showroom/Kho";
    public const string CustomerCare = "CSKH";

    public static readonly IReadOnlyList<string> All =
    [
        Admin,
        Director,
        Accountant,
        Sales,
        WarehouseKeeper,
        ShowroomWarehouseLead,
        CustomerCare
    ];

    public static bool RequiresLocation(string? role) =>
        role is WarehouseKeeper or ShowroomWarehouseLead or Sales;

    public static bool IsAllowed(string? role) =>
        role is not null && All.Contains(role, StringComparer.Ordinal);
}
