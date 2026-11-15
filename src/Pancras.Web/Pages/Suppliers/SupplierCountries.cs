namespace Pancras.Web.Pages.Suppliers;

/// <summary>Danh sách quốc gia/khu vực thường gặp của nhà cung cấp nhập khẩu, để chọn thay vì gõ tay (tránh "Trung Quốc", "China", "TQ" lẫn lộn).</summary>
public static class SupplierCountries
{
    public static readonly IReadOnlyList<string> Common =
    [
        "Trung Quốc",
        "Đài Loan",
        "Hồng Kông",
        "Hàn Quốc",
        "Nhật Bản",
        "Thái Lan",
        "Malaysia",
        "Indonesia",
        "Singapore",
        "Ấn Độ",
        "Thổ Nhĩ Kỳ",
        "Ý",
        "Tây Ban Nha",
        "Đức",
        "Pháp",
        "Bồ Đào Nha",
        "Ba Lan",
        "Cộng hòa Séc",
        "Thụy Sĩ",
        "Vương quốc Anh",
        "Hoa Kỳ",
        "Brazil",
        "Các Tiểu vương quốc Ả Rập Thống nhất",
        "Việt Nam"
    ];

    /// <summary>Danh sách để chọn trong form. Giá trị cũ không nằm trong danh sách (dữ liệu nhập tay trước đây) vẫn được giữ để không mất khi sửa.</summary>
    public static IReadOnlyList<string> For(string? current)
    {
        var value = current?.Trim();
        return string.IsNullOrEmpty(value) || Common.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? Common
            : [.. Common, value];
    }
}
