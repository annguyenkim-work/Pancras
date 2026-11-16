using System.Globalization;

namespace Pancras.Web.Formatting;

/// <summary>
/// Giờ hiển thị cố định theo Việt Nam (UTC+7, không có giờ mùa hè), không phụ thuộc múi giờ của máy chủ.
/// Dữ liệu lưu UTC; chỉ đổi khi hiển thị.
/// </summary>
public static class VietnamTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTimeOffset Of(DateTimeOffset time) => time.ToOffset(Offset);

    public static DateOnly DateOf(DateTimeOffset time) => DateOnly.FromDateTime(Of(time).DateTime);

    public static string Format(DateTimeOffset time, string format = "dd/MM/yyyy HH:mm") =>
        Of(time).ToString(format, CultureInfo.InvariantCulture);
}
