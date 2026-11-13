using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pancras.Web.Pages;

public class ComingSoonModel : PageModel
{
    public const string Message = "Chức năng đang được triển khai";

    public static readonly IReadOnlyList<string> Routes =
    [
        "/customers",
        "/sales",
        "/quotes",
        "/combos",
        "/receivables",
        "/warranty",
        "/locations",
        "/audit"
    ];

    private static readonly Dictionary<string, string> Titles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/customers"] = "Danh bạ khách hàng",
        ["/sales"] = "Bán hàng (Lẻ - Đại lý - Dự án)",
        ["/quotes"] = "Báo giá & Catalog",
        ["/combos"] = "Quản lý Combo Sản phẩm",
        ["/receivables"] = "Quản lý Công nợ & Hạn mức",
        ["/warranty"] = "Bảo hành Điện tử (Serial/SĐT)",
        ["/locations"] = "Kho & Showroom",
        ["/audit"] = "Nhật ký Hệ thống (Audit Log)"
    };

    public string Title { get; private set; } = Message;

    public void OnGet()
    {
        var path = HttpContext.Request.Path.Value ?? string.Empty;
        if (Titles.TryGetValue(path, out var title))
        {
            Title = title;
        }
    }
}
