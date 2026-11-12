using System.Globalization;
using System.Text;
using Pancras.Application.Catalog;

namespace Pancras.Web.Formatting;

public static class CatalogCsv
{
    public static byte[] Write(IReadOnlyList<CatalogItemRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Mã,Tên,Ngành,Thương hiệu,Đơn vị,Giá vốn,Giá lẻ,Giá đại lý,Giá nhà phân phối,Giá dự án");
        foreach (var row in rows)
        {
            builder.Append(Cell(row.Code)).Append(',');
            builder.Append(Cell(row.Name)).Append(',');
            builder.Append(Cell(CatalogLabels.Category(row.Category))).Append(',');
            builder.Append(Cell(row.Brand)).Append(',');
            builder.Append(Cell(row.Unit)).Append(',');
            builder.Append(Whole(row.CostPrice)).Append(',');
            builder.Append(Whole(row.RetailPrice)).Append(',');
            builder.Append(Whole(row.DealerPrice)).Append(',');
            builder.Append(Whole(row.DistributorPrice)).Append(',');
            builder.Append(Whole(row.ProjectPrice));
            builder.AppendLine();
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static string Whole(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    private static string Cell(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
