using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>Số biên bản kiểm kê, phiếu điều chỉnh, phiếu hủy người dùng được thấy (đếm trong database).</summary>
public sealed record StockDocumentCounts(int Stocktakes, int Adjustments, int Disposals)
{
    public int Total => Stocktakes + Adjustments + Disposals;
}

/// <summary>Vị trí một phiếu trong danh sách gộp: loại phiếu và thứ tự (từ 0) trong loại đó, mới nhất trước.</summary>
public sealed record StockDocumentPosition(StockDocumentKind Kind, int Index);

/// <summary>Đoạn cần lấy của một loại phiếu: bỏ qua <paramref name="Skip"/> phiếu đầu, lấy <paramref name="Take"/> phiếu.</summary>
public readonly record struct StockDocumentRange(int Skip, int Take);

/// <summary>Trang hiện tại của danh sách gộp và đoạn cần lấy ở từng loại phiếu.</summary>
public sealed record StockDocumentWindowResult(int Page, StockDocumentRange Stocktakes, StockDocumentRange Adjustments, StockDocumentRange Disposals);

/// <summary>
/// Tab Kiểm kê và điều chỉnh gộp ba loại phiếu thành một danh sách: kiểm kê trước, rồi điều chỉnh, rồi hủy (mỗi loại mới nhất trước).
/// Tính trang và đoạn cần lấy ở từng bảng từ số đếm, để database chỉ trả các phiếu của trang. Phiếu được chỉ định (mở từ thông báo)
/// thì nhảy tới đúng trang chứa nó.
/// </summary>
public static class StockDocumentWindow
{
    public static StockDocumentWindowResult Compute(StockDocumentCounts counts, int page, int size, StockDocumentPosition? focus)
    {
        var pageSize = size < 1 ? Pancras.Application.Common.PageSizes.Default : size;
        var pages = Math.Max(1, (counts.Total + pageSize - 1) / pageSize);
        var current = Math.Clamp(page, 1, pages);
        if (focus is not null)
        {
            var position = OffsetOf(counts, focus.Kind) + focus.Index;
            current = (position / pageSize) + 1;
        }

        var from = (current - 1) * pageSize;
        var to = Math.Min(counts.Total, from + pageSize);
        return new StockDocumentWindowResult(
            current,
            Range(0, counts.Stocktakes, from, to),
            Range(counts.Stocktakes, counts.Adjustments, from, to),
            Range(counts.Stocktakes + counts.Adjustments, counts.Disposals, from, to));
    }

    private static int OffsetOf(StockDocumentCounts counts, StockDocumentKind kind) => kind switch
    {
        StockDocumentKind.Adjustment => counts.Stocktakes,
        StockDocumentKind.Disposal => counts.Stocktakes + counts.Adjustments,
        _ => 0
    };

    /// <summary>Phần của một loại (bắt đầu ở vị trí <paramref name="offset"/> trong dãy gộp, có <paramref name="count"/> phiếu) nằm trong [from, to).</summary>
    private static StockDocumentRange Range(int offset, int count, int from, int to)
    {
        var start = Math.Max(from - offset, 0);
        var end = Math.Min(to - offset, count);
        return start >= end ? new StockDocumentRange(0, 0) : new StockDocumentRange(start, end - start);
    }
}

/// <summary>Một trang của tab Kiểm kê và điều chỉnh: các phiếu từng loại, tổng số phiếu và số trang đã dùng.</summary>
public sealed record StockDocumentPage(
    IReadOnlyList<StocktakeRow> Stocktakes,
    IReadOnlyList<AdjustmentRow> Adjustments,
    IReadOnlyList<DisposalRow> Disposals,
    int Total,
    int Page);

public enum StockDocumentLabelKind
{
    Transfer,
    Stocktake,
    Adjustment,
    Disposal
}

/// <summary>Đủ thông tin để ghi chú một dòng lịch sử tồn hoặc một liên kết tới phiếu (mã phiếu chuyển, tuyến, ghi chú/lý do).</summary>
public sealed record StockDocumentLabel(StockDocumentLabelKind Kind, int TransferNumber, string SourceName, string DestinationName, string Text);
