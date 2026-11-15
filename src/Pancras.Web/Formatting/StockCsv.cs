using System.Globalization;
using System.Text;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Web.Formatting;

public static class StockLabels
{
    public static string Movement(StockMovementKind kind) => kind switch
    {
        StockMovementKind.Receipt => "Nhập",
        StockMovementKind.Issue => "Xuất",
        StockMovementKind.Transfer => "Chuyển",
        StockMovementKind.Adjustment => "Điều chỉnh",
        _ => string.Empty
    };

    public static string Transfer(TransferStatus status) => status switch
    {
        TransferStatus.Draft => "Nháp",
        TransferStatus.InTransit => "Đang chuyển",
        TransferStatus.Received => "Đã nhận",
        _ => string.Empty
    };

    public static string Approval(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Pending => "Chờ duyệt",
        ApprovalStatus.Approved => "Đã duyệt",
        ApprovalStatus.Recorded => "Đã ghi nhận",
        _ => string.Empty
    };

    public static string TransferPill(TransferStatus status) => status switch
    {
        TransferStatus.Draft => "Nháp",
        TransferStatus.Requested => "Chờ duyệt",
        TransferStatus.Approved => "Đã duyệt, chờ xuất kho",
        TransferStatus.Rejected => "Bị từ chối",
        TransferStatus.InTransit => "Đang vận chuyển",
        TransferStatus.Received => "Đã nhận hàng",
        TransferStatus.ReceivedWithDiscrepancy => "Nhận có chênh lệch",
        TransferStatus.Cancelled => "Đã hủy",
        TransferStatus.Returned => "Đã trả về kho xuất",
        _ => string.Empty
    };

    /// <summary>Màu nhãn trạng thái: vàng chờ duyệt, xanh dương đã duyệt, đỏ từ chối, cam chênh lệch, xanh lá hoàn tất.</summary>
    public static string TransferPillClass(TransferStatus status) => status switch
    {
        TransferStatus.Requested => "bg-yellow-100 text-yellow-800",
        TransferStatus.Approved => "bg-sky-100 text-sky-800",
        TransferStatus.Rejected => "bg-rose-100 text-rose-800",
        TransferStatus.InTransit => "bg-amber-100 text-amber-800",
        TransferStatus.Received => "bg-emerald-100 text-emerald-800",
        TransferStatus.ReceivedWithDiscrepancy => "bg-orange-100 text-orange-800",
        TransferStatus.Cancelled => "bg-slate-200 text-slate-600",
        TransferStatus.Returned => "bg-violet-100 text-violet-800",
        _ => "bg-slate-100 text-slate-600"
    };

    public static string TransferLeg(TransferStatus status) => status switch
    {
        TransferStatus.Requested => "chờ duyệt",
        TransferStatus.Approved => "chờ xuất kho",
        TransferStatus.InTransit => "đang trên xe",
        TransferStatus.Received => "đã hoàn tất",
        TransferStatus.ReceivedWithDiscrepancy => "đã nhận, có chênh lệch",
        TransferStatus.Returned => "đã trả về",
        TransferStatus.Cancelled => "đã hủy",
        TransferStatus.Rejected => "bị từ chối",
        _ => "chưa xuất xe"
    };

    public static string TransferEvent(TransferEventKind kind) => kind switch
    {
        TransferEventKind.Requested => "Lập phiếu đề nghị",
        TransferEventKind.Edited => "Sửa phiếu",
        TransferEventKind.Approved => "Duyệt",
        TransferEventKind.Rejected => "Từ chối",
        TransferEventKind.Shipped => "Xuất kho",
        TransferEventKind.Received => "Nhận hàng",
        TransferEventKind.ReceivedWithDiscrepancy => "Nhận hàng, có chênh lệch",
        TransferEventKind.Resolved => "Xử lý chênh lệch",
        TransferEventKind.Cancelled => "Hủy phiếu",
        TransferEventKind.Returned => "Trả hàng về kho xuất",
        _ => string.Empty
    };

    /// <summary>Các bước chính của một phiếu, theo thứ tự, để vẽ thanh tiến trình.</summary>
    public static IReadOnlyList<string> TransferSteps { get; } = ["Lập phiếu", "Duyệt", "Xuất kho", "Nhận hàng", "Xử lý chênh lệch"];

    /// <summary>Số bước đã xong (0 đến 5) và số bước hiện tại trên thanh tiến trình, theo trạng thái phiếu.</summary>
    public static int TransferProgress(TransferStatus status) => status switch
    {
        TransferStatus.Requested => 1,
        TransferStatus.Approved => 2,
        TransferStatus.InTransit => 3,
        TransferStatus.Received => 5,
        TransferStatus.ReceivedWithDiscrepancy => 4,
        TransferStatus.Returned => 3,
        _ => 0
    };

    public static string StocktakePill(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Approved => "Đã duyệt cập nhật tồn",
        _ => "Chờ duyệt"
    };

    public static (string Label, string ClassName) MovementBadge(StockMovementKind kind, int quantity) => kind switch
    {
        StockMovementKind.Issue => ("Xuất bán", "bg-sky-100 text-sky-800"),
        StockMovementKind.Transfer when quantity < 0 => ("Chuyển đi", "bg-amber-100 text-amber-800"),
        StockMovementKind.Transfer => ("Nhận về", "bg-violet-100 text-violet-800"),
        StockMovementKind.Adjustment => ("Điều chỉnh", "bg-rose-100 text-rose-800"),
        StockMovementKind.Receipt => ("Nhập", "bg-emerald-100 text-emerald-800"),
        _ => (string.Empty, "bg-slate-100 text-slate-700")
    };

    public static string LocationCode(string name, StockLocationKind kind)
    {
        var prefix = kind == StockLocationKind.Showroom ? "S" : "K";
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "kho", "showroom", "tổng", "và", "tai", "tại"
        };
        var initials = new StringBuilder();
        foreach (var word in name.Split([' ', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (skip.Contains(word))
            {
                continue;
            }

            initials.Append(char.ToUpperInvariant(word[0]));
            if (initials.Length == 4)
            {
                break;
            }
        }

        if (initials.Length == 0)
        {
            var trimmed = name.Trim();
            initials.Append(trimmed.Length == 0 ? "X" : char.ToUpperInvariant(trimmed[0]));
        }

        return prefix + "-" + initials;
    }

    public static DateTimeOffset? Version7Time(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!id.TryWriteBytes(bytes, bigEndian: true, out _))
        {
            return null;
        }

        if ((bytes[6] & 0xF0) != 0x70)
        {
            return null;
        }

        var unixMs =
            ((long)bytes[0] << 40)
            | ((long)bytes[1] << 32)
            | ((long)bytes[2] << 24)
            | ((long)bytes[3] << 16)
            | ((long)bytes[4] << 8)
            | bytes[5];
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static string DocumentCode(string prefix, Guid id) => StockDocumentCode.Of(prefix, id);
}

public static class StockCsv
{
    public static byte[] Write(IReadOnlyList<StockBalanceRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Mã,Tên,Thương hiệu,Ngành,Địa điểm,Tồn thực tế,Đang giữ chỗ,Khả dụng,Đang vận chuyển");
        foreach (var row in rows)
        {
            builder.Append(Cell(row.Code)).Append(',');
            builder.Append(Cell(row.Name)).Append(',');
            builder.Append(Cell(row.Brand)).Append(',');
            builder.Append(Cell(CatalogLabels.Category(row.Category))).Append(',');
            builder.Append(Cell(row.LocationName)).Append(',');
            builder.Append(row.OnHand.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.Reserved.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.Available.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(row.InTransit.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static string Cell(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
