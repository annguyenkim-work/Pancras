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

    public static string FormatMeasure(decimal amount) => amount.ToString("0.###", Vietnamese);
}
