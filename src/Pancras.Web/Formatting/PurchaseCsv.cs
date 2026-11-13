using System.Globalization;
using System.Text;
using Pancras.Application.Purchasing;

namespace Pancras.Web.Formatting;

public static class PurchaseCsv
{
    public static byte[] Build(PurchaseOrderDetail order)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Mã,Tên,Số lượng,FOB USD,Giá vốn đơn vị");
        foreach (var line in order.Lines)
        {
            builder
                .Append(Escape(line.ItemCode)).Append(',')
                .Append(Escape(line.ItemName)).Append(',')
                .Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(line.FobUsd.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(line.Cost.UnitCost.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
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
