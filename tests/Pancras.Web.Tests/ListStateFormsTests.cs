using System.Text.RegularExpressions;

namespace Pancras.Web.Tests;

/// <summary>
/// Lỗi UAT: duyệt, nhận hàng (một phần, đủ) và các thao tác chuyển kho tải lại trang mà mất bộ lọc, vì form chỉ gửi mã đối tượng.
/// Luật (docs/03-ui-ux/components.md mục 10): form thao tác ở mọi trang danh sách (Mua hàng, Kho, Danh mục, Nhân viên, Nhà cung cấp, Kho & Showroom) lấy action từ hàm dựng địa chỉ của trang,
/// hàm đó mang bộ lọc, trang và cỡ trang; form có action tự đặt phải bật antiforgery.
/// </summary>
public class ListStateFormsTests
{
    private static readonly string Pages = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Pancras.Web/Pages"));

    private static readonly string[] Views =
    [
        "Purchasing/_OrderDetail.cshtml",
        "Purchasing/_OrderForm.cshtml",
        "Inventory/_Dialogs.cshtml",
        "Inventory/_Stocktakes.cshtml",
        "Inventory/_Transfers.cshtml",
        "Catalog/Index.cshtml",
        "Employees/Index.cshtml",
        "Suppliers/Index.cshtml",
        "Locations/Index.cshtml"
    ];

    private static IEnumerable<(string View, string Tag)> PostForms() =>
        Views.SelectMany(view => Regex.Matches(File.ReadAllText(Path.Combine(Pages, view)), @"<form\b[^>]*method=""post""[^>]*>")
            .Select(match => (view, match.Value)));

    [Fact]
    public void TheScan_FindsTheActionForms()
    {
        // Test đối chứng: nếu regex không khớp gì thì test dưới xanh giả.
        Assert.True(PostForms().Count() >= 37);
    }

    [Fact]
    public void ActionForms_TakeTheirActionFromTheListStateHelper_WithAntiforgery()
    {
        var offenders = PostForms()
            .Where(form => !Regex.IsMatch(form.Tag, @"action=""@Model\.(ActionUrl|TransferActionUrl|StocktakeActionUrl)\(")
                || !form.Tag.Contains("asp-antiforgery=\"true\"", StringComparison.Ordinal)
                || form.Tag.Contains("asp-page-handler", StringComparison.Ordinal))
            .Select(form => form.View + ": " + form.Tag[..Math.Min(140, form.Tag.Length)])
            .ToArray();

        Assert.True(offenders.Length == 0, "Form thao tác phải mang bộ lọc của danh sách:\n" + string.Join("\n", offenders));
    }
}
