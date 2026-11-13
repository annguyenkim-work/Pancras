using System.Globalization;
using System.Text;
using Pancras.Application.Purchasing;

namespace Pancras.Web.Formatting;

public static class PurchaseCsv
{
    /// <param name="includeCost">Sai thì file chỉ có mã, tên, số lượng (vai trò không được xem giá FOB và giá vốn).</param>
    public static byte[] Build(PurchaseOrderDetail order, bool includeCost = true)
    {
        var builder = new StringBuilder();
        builder.AppendLine(includeCost ? "Mã,Tên,Số lượng,FOB USD,Giá vốn đơn vị" : "Mã,Tên,Số lượng");
        foreach (var line in order.Lines)
        {
            builder
                .Append(Escape(line.ItemCode)).Append(',')
                .Append(Escape(line.ItemName)).Append(',')
                .Append(line.Quantity.ToString(CultureInfo.InvariantCulture));
            if (includeCost)
            {
                builder
                    .Append(',').Append(line.FobUsd.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(line.Cost.UnitCost.ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
        }

        // Có BOM để Excel đọc đúng tiếng Việt.
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        return [.. Encoding.UTF8.GetPreamble(), .. body];
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
