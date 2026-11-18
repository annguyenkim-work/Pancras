using System.Globalization;

namespace Pancras.Web.Formatting;

public static class VndMoney
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static string FormatDong(decimal amount)
    {
        var whole = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        return whole.ToString("N0", Vietnamese) + " VNĐ";
    }

    /// <summary>Tiền USD: tối đa 2 số lẻ, dấu phân cách vi-VN, hậu tố <c>USD</c>.</summary>
    public static string FormatUsd(decimal amount) => amount.ToString("#,##0.##", Vietnamese) + " USD";

    /// <summary>Phần trăm từ một tỷ lệ (0,1 thành 10%), tối đa 4 số lẻ.</summary>
    public static string FormatPercent(decimal fraction) => (fraction * 100m).ToString("0.####", Vietnamese) + "%";

    public static string FormatMeasure(decimal amount) => amount.ToString("0.###", Vietnamese);

    /// <summary>% chiết khấu: bỏ số 0 thừa, dùng dấu phẩy thập phân (20 hoặc 12,5); trống nếu chưa cấu hình.</summary>
    public static string FormatPercent(decimal? percent) => percent is null ? string.Empty : percent.Value.ToString("0.##", Vietnamese);
}
