using Pancras.Domain.Employees;

namespace Pancras.Web.Help;

/// <summary>Một ô trong bảng phân quyền: có quyền không, và điều kiện đi kèm (ví dụ "Kho mình").</summary>
public sealed record HelpGrant(bool Allowed, string? Note = null)
{
    public static HelpGrant Yes { get; } = new(true);

    public static HelpGrant No { get; } = new(false);

    public static HelpGrant Scoped(string note) => new(true, note);
}

/// <summary>Một dòng của bảng phân quyền: chức năng và quyền theo từng vai, tính bằng chính hàm Access đang kiểm quyền.</summary>
public sealed record HelpCapability(string Name, Func<string, HelpGrant> For);

/// <summary>Nội dung hướng dẫn của một màn hình: mục tiêu, chức năng, quy trình, phân quyền.</summary>
public sealed record HelpTopic(
    string Key,
    string Title,
    string Objective,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Workflow,
    IReadOnlyList<HelpCapability> Permissions);

public sealed record HelpRoleColumn(string Role, string Label);

/// <summary>Các vai trò hiện trong bảng phân quyền, theo thứ tự trong docs/04-domain/roles-and-permissions.md.</summary>
public static class HelpRoles
{
    public static IReadOnlyList<HelpRoleColumn> Columns { get; } =
    [
        new(EmployeeRole.Admin, "Admin"),
        new(EmployeeRole.Director, "Giám đốc"),
        new(EmployeeRole.Accountant, "Kế toán"),
        new(EmployeeRole.ShowroomWarehouseLead, "Trưởng Showroom/Kho"),
        new(EmployeeRole.WarehouseKeeper, "Thủ kho"),
        new(EmployeeRole.Sales, "Sales")
    ];
}

/// <summary>
/// Tính quyền theo vai bằng cách gọi đúng hàm Access của module với một người dùng mẫu, nên bảng phân quyền
/// không bao giờ lệch khỏi quyền thật. Hàm kiểm nhận (người dùng, kho của chứng từ).
/// </summary>
internal static class HelpProbe
{
    /// <summary>Có quyền ở mọi kho: có. Chỉ có quyền khi chứng từ ở kho mình: có, kèm ghi chú. Còn lại: không.</summary>
    public static HelpGrant Scope(string role, Func<EmployeeActor, Guid, bool> check, string scopedNote)
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();
        var actor = new EmployeeActor(Guid.NewGuid(), role, own);
        if (check(actor, other))
        {
            return HelpGrant.Yes;
        }

        return check(actor, own) ? HelpGrant.Scoped(scopedNote) : HelpGrant.No;
    }

    public static HelpGrant Role(string role, Func<string, bool> check, string? note = null) =>
        check(role) ? (note is null ? HelpGrant.Yes : HelpGrant.Scoped(note)) : HelpGrant.No;
}
