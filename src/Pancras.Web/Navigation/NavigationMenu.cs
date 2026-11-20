using Pancras.Domain.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Web.Navigation;

/// <summary>Một mục menu. <paramref name="IsVisible"/> null nghĩa là mọi người dùng đã đăng nhập đều thấy.</summary>
public sealed record NavItem(
    string Group,
    string Href,
    string Label,
    Func<string?, bool>? IsVisible = null,
    bool ComingSoon = false,
    string? HomeTitle = null,
    string? HomeHint = null)
{
    public bool VisibleTo(string? role) => IsVisible is null || IsVisible(role);

    /// <summary>Thẻ lối tắt ở trang chủ: chỉ mục đã làm, có mô tả.</summary>
    public bool OnHome => !ComingSoon && HomeHint is not null;
}

/// <summary>Danh sách dữ liệu duy nhất của menu, lối tắt trang chủ và tiêu đề trang "sắp có". Thêm module = thêm một dòng.</summary>
public static class NavigationMenu
{
    public const string System = "HỆ THỐNG";
    public const string Warehouse = "VẬN HÀNH KHO BÃI";
    public const string Sales = "KINH DOANH & PHÂN PHỐI";
    public const string Finance = "TÀI CHÍNH";
    public const string Service = "DỊCH VỤ KHÁCH HÀNG";
    public const string Admin = "QUẢN TRỊ NỘI BỘ";

    /// <summary>Thứ tự nhóm cố định (docs/03-ui-ux/app-shell.md).</summary>
    public static readonly IReadOnlyList<string> Groups = [System, Warehouse, Sales, Finance, Service, Admin];

    public static readonly IReadOnlyList<NavItem> Items =
    [
        new(System, "/", "Tổng quan điều hành"),
        new(Warehouse, "/inventory", "Quản lý Kho & Điều chuyển", InventoryAccess.CanView,
            HomeTitle: "Kho & Điều chuyển", HomeHint: "Tra cứu tồn, chuyển kho, kiểm kê, điều chỉnh."),
        new(Warehouse, "/purchasing", "Mua hàng & Container Nhập", PurchaseAccess.CanOpenPage,
            HomeTitle: "Mua hàng & Container", HomeHint: "Lập đơn nhập khẩu, phân bổ chi phí, nhận hàng."),
        new(Warehouse, "/suppliers", "Nhà cung cấp nhập khẩu", SupplierAccess.CanView,
            HomeTitle: "Nhà cung cấp", HomeHint: "Danh bạ nhà cung cấp nhập khẩu."),
        new(Sales, "/catalog", "Danh mục sản phẩm & giá", CatalogAccess.CanView,
            HomeHint: "Mặt hàng, quy cách và các bảng giá."),
        new(Sales, "/customers", "Danh bạ khách hàng", ComingSoon: true),
        new(Sales, "/sales", "Bán hàng (Lẻ - Đại lý - Dự án)", ComingSoon: true),
        new(Sales, "/quotes", "Báo giá & Catalog", ComingSoon: true),
        new(Sales, "/combos", "Quản lý Combo Sản phẩm", ComingSoon: true),
        new(Finance, "/receivables", "Quản lý Công nợ & Hạn mức", ComingSoon: true),
        new(Service, "/warranty", "Bảo hành Điện tử (Serial/SĐT)", ComingSoon: true),
        new(Admin, "/employees", "Nhân sự & Phân quyền", EmployeeAccess.CanList,
            HomeHint: "Hồ sơ nhân viên và vai trò."),
        new(Admin, "/locations", "Kho & Showroom", LocationAccess.CanView),
        new(Admin, "/audit", "Nhật ký Hệ thống (Audit Log)", AuditAccess.CanView),
    ];

    public static IEnumerable<NavItem> VisibleFor(string? role) => Items.Where(item => item.VisibleTo(role));

    public static IEnumerable<NavItem> ShortcutsFor(string? role) => VisibleFor(role).Where(item => item.OnHome);

    public static bool IsComingSoon(string path) =>
        Items.Any(item => item.ComingSoon && string.Equals(item.Href, path, StringComparison.OrdinalIgnoreCase));

    public static string? TitleOf(string path) =>
        Items.FirstOrDefault(item => string.Equals(item.Href, path, StringComparison.OrdinalIgnoreCase))?.Label;
}
