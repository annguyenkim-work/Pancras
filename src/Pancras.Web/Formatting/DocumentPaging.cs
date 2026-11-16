using Pancras.Application.Inventory;

namespace Pancras.Web.Formatting;

/// <summary>Các phiếu của một trang ở tab Kiểm kê và điều chỉnh, cùng tổng số phiếu và số trang đã dùng.</summary>
public sealed record DocumentSlice(
    IReadOnlyList<StocktakeRow> Stocktakes,
    IReadOnlyList<AdjustmentRow> Adjustments,
    IReadOnlyList<DisposalRow> Disposals,
    int Total,
    int Page);

/// <summary>
/// Tab Kiểm kê và điều chỉnh gộp ba loại phiếu thành một danh sách phân trang: kiểm kê trước, rồi điều chỉnh, rồi hủy
/// (mỗi loại giữ thứ tự có sẵn). Phiếu được chỉ định (từ thông báo) thì nhảy tới đúng trang chứa nó.
/// </summary>
public static class DocumentPaging
{
    public static DocumentSlice Slice(
        IReadOnlyList<StocktakeRow> stocktakes,
        IReadOnlyList<AdjustmentRow> adjustments,
        IReadOnlyList<DisposalRow> disposals,
        int page,
        int size,
        Guid? focus)
    {
        var total = stocktakes.Count + adjustments.Count + disposals.Count;
        var pageSize = size < 1 ? Pancras.Application.Common.PageSizes.Default : size;
        var pages = Math.Max(1, (total + pageSize - 1) / pageSize);
        var current = Math.Clamp(page, 1, pages);
        if (focus is Guid id)
        {
            var position = IndexOf(stocktakes, adjustments, disposals, id);
            if (position >= 0)
            {
                current = (position / pageSize) + 1;
            }
        }

        var from = (current - 1) * pageSize;
        var to = Math.Min(total, from + pageSize);
        return new DocumentSlice(
            Range(stocktakes, 0, from, to),
            Range(adjustments, stocktakes.Count, from, to),
            Range(disposals, stocktakes.Count + adjustments.Count, from, to),
            total,
            current);
    }

    private static int IndexOf(
        IReadOnlyList<StocktakeRow> stocktakes,
        IReadOnlyList<AdjustmentRow> adjustments,
        IReadOnlyList<DisposalRow> disposals,
        Guid id)
    {
        for (var i = 0; i < stocktakes.Count; i++)
        {
            if (stocktakes[i].Id == id)
            {
                return i;
            }
        }

        for (var i = 0; i < adjustments.Count; i++)
        {
            if (adjustments[i].Id == id)
            {
                return stocktakes.Count + i;
            }
        }

        for (var i = 0; i < disposals.Count; i++)
        {
            if (disposals[i].Id == id)
            {
                return stocktakes.Count + adjustments.Count + i;
            }
        }

        return -1;
    }

    /// <summary>Phần của danh sách nằm trong khoảng [from, to) của dãy gộp, biết danh sách này bắt đầu ở vị trí offset.</summary>
    private static IReadOnlyList<T> Range<T>(IReadOnlyList<T> list, int offset, int from, int to)
    {
        var start = Math.Max(from - offset, 0);
        var end = Math.Min(to - offset, list.Count);
        return start >= end ? [] : list.Skip(start).Take(end - start).ToArray();
    }
}
