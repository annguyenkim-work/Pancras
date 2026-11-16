using Pancras.Application.Common;

namespace Pancras.Web.Formatting;

/// <summary>Dữ liệu cho partial <c>_Pager</c>. <c>UrlFor</c> nhận (số trang, cỡ trang) và trả địa chỉ giữ nguyên bộ lọc.</summary>
public sealed record PagerModel(int PageNumber, int PageSize, int Total, Func<int, int, string> UrlFor)
{
    public int PageCount => Total == 0 ? 1 : (Total + PageSize - 1) / PageSize;

    public int First => Total == 0 ? 0 : (PageNumber - 1) * PageSize + 1;

    public int Last => Math.Min(Total, PageNumber * PageSize);

    public IReadOnlyList<int> VisiblePages
    {
        get
        {
            const int window = 5;
            var start = Math.Max(1, PageNumber - window / 2);
            var end = Math.Min(PageCount, start + window - 1);
            start = Math.Max(1, end - window + 1);
            return Enumerable.Range(start, end - start + 1).ToArray();
        }
    }

    /// <summary>Giá trị tham số từ query, không có thì từ form (để giữ sau thao tác POST).</summary>
    public static string? TokenFrom(HttpRequest request, string name)
    {
        if (request.Query.TryGetValue(name, out var fromQuery) && !string.IsNullOrEmpty(fromQuery))
        {
            return fromQuery.ToString();
        }

        return request.HasFormContentType && request.Form.TryGetValue(name, out var fromForm) ? fromForm.ToString() : null;
    }

    public static int SizeFrom(HttpRequest request) =>
        PageSizes.Normalize(int.TryParse(TokenFrom(request, "size"), out var size) ? size : null);
}
