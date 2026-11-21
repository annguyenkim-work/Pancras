using System.Globalization;

namespace Pancras.Web.Formatting;

/// <summary>
/// Dựng địa chỉ của một danh sách (đường dẫn + tham số lọc, sắp xếp, cỡ trang, trang) ở một nơi, dùng cho link, phân trang,
/// form thao tác (<see cref="WithHandler"/>) và chuyển trang sau POST (docs/03-ui-ux/components.md mục 10).
/// Tham số giữ đúng thứ tự thêm; giá trị rỗng hoặc bằng mặc định thì bỏ; giá trị được cắt khoảng trắng và mã hóa.
/// </summary>
public sealed class ListQuery(string path)
{
    private readonly List<string> _parts = [];

    public string Path { get; } = path;

    /// <summary>Phần sau dấu <c>?</c>, không có dấu <c>?</c>; rỗng khi không có tham số.</summary>
    public string Query => string.Join('&', _parts);

    public ListQuery Add(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _parts.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }

        return this;
    }

    public ListQuery Add(string name, Guid? value) =>
        value is Guid id && id != Guid.Empty ? Add(name, id.ToString("D")) : this;

    /// <summary>Thêm <c>name=true</c> khi cờ bật.</summary>
    public ListQuery Add(string name, bool flag) => flag ? Add(name, "true") : this;

    /// <summary>Thêm số khi khác giá trị mặc định (ví dụ cỡ trang 20, trang 1).</summary>
    public ListQuery Add(string name, int value, int defaultValue) =>
        value == defaultValue ? this : Add(name, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Thêm trang khi lớn hơn 1.</summary>
    public ListQuery AddPage(string name, int page) =>
        page > 1 ? Add(name, page.ToString(CultureInfo.InvariantCulture)) : this;

    /// <summary>Địa chỉ form thao tác: <c>path?handler=X&amp;...</c>; handler null là handler mặc định của trang.</summary>
    public string WithHandler(string? handler)
    {
        var parts = string.IsNullOrEmpty(handler) ? _parts : _parts.Prepend("handler=" + Uri.EscapeDataString(handler));
        var query = string.Join('&', parts);
        return query.Length == 0 ? Path : Path + "?" + query;
    }

    public override string ToString() => _parts.Count == 0 ? Path : Path + "?" + Query;
}
