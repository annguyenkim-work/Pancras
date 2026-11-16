using Pancras.Application.Common;

namespace Pancras.Web.Formatting;

/// <summary>Chuyển trang kiểu keyset (Trước/Sau, không đánh số trang, không tổng) cho danh sách rất lớn như nhật ký.</summary>
public sealed record KeysetPagerModel(
    int Shown,
    int PageSize,
    string? PreviousUrl,
    string? NextUrl,
    Func<int, string> SizeUrlFor)
{
    public IReadOnlyList<int> Sizes => PageSizes.Allowed;
}
