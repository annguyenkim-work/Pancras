using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Tests.Shared;

/// <summary>
/// Phần dùng chung của các kho giả trong Application.Tests và Web.Tests cho tab Kiểm kê và điều chỉnh: phạm vi kho, bộ lọc loại và
/// mã phiếu, mới nhất trước. Làm giống <c>InventoryRepository</c> (mã là 8 ký tự hex cuối của Id, khớp không phân biệt hoa thường).
/// File này được liên kết vào cả hai project test, sửa một chỗ cho cả hai.
/// </summary>
internal static class FakeStockDocuments
{
    private static readonly StockDocumentFilter All = new();

    public static IEnumerable<StocktakeSheet> Stocktakes(IEnumerable<StocktakeSheet> all, Guid? scope, StockDocumentFilter? filter = null) =>
        Apply(all, scope, filter ?? All, StockDocumentKind.Stocktake, StockDocumentCode.Stocktake, sheet => sheet.Id, sheet => sheet.StockLocationId);

    public static IEnumerable<QuantityAdjustment> Adjustments(IEnumerable<QuantityAdjustment> all, Guid? scope, StockDocumentFilter? filter = null) =>
        Apply(all, scope, filter ?? All, StockDocumentKind.Adjustment, StockDocumentCode.Adjustment, item => item.Id, item => item.StockLocationId);

    public static IEnumerable<DisposalSlip> Disposals(IEnumerable<DisposalSlip> all, Guid? scope, StockDocumentFilter? filter = null) =>
        Apply(all, scope, filter ?? All, StockDocumentKind.Disposal, StockDocumentCode.Disposal, slip => slip.Id, slip => slip.StockLocationId);

    public static StockDocumentCounts Count(
        IEnumerable<StocktakeSheet> stocktakes, IEnumerable<QuantityAdjustment> adjustments, IEnumerable<DisposalSlip> disposals, Guid? scope, StockDocumentFilter filter) =>
        new(Stocktakes(stocktakes, scope, filter).Count(), Adjustments(adjustments, scope, filter).Count(), Disposals(disposals, scope, filter).Count());

    public static StockDocumentPosition? Locate(
        IEnumerable<StocktakeSheet> stocktakes, IEnumerable<QuantityAdjustment> adjustments, IEnumerable<DisposalSlip> disposals, Guid? scope, StockDocumentFilter filter, Guid id)
    {
        var stocktake = Stocktakes(stocktakes, scope, filter).Select(sheet => sheet.Id).ToList().IndexOf(id);
        if (stocktake >= 0)
        {
            return new StockDocumentPosition(StockDocumentKind.Stocktake, stocktake);
        }

        var adjustment = Adjustments(adjustments, scope, filter).Select(item => item.Id).ToList().IndexOf(id);
        if (adjustment >= 0)
        {
            return new StockDocumentPosition(StockDocumentKind.Adjustment, adjustment);
        }

        var disposal = Disposals(disposals, scope, filter).Select(slip => slip.Id).ToList().IndexOf(id);
        return disposal >= 0 ? new StockDocumentPosition(StockDocumentKind.Disposal, disposal) : null;
    }

    private static IEnumerable<T> Apply<T>(
        IEnumerable<T> all, Guid? scope, StockDocumentFilter filter, StockDocumentKind kind, string prefix, Func<T, Guid> id, Func<T, Guid> location)
    {
        if (!filter.Includes(kind))
        {
            return [];
        }

        var matching = all.Where(document => scope is not Guid only || location(document) == only);
        if (filter.CodeFragment is { } fragment)
        {
            matching = matching.Where(document =>
                StockDocumentCode.Of(prefix, id(document))[(prefix.Length + 1)..].Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }

        return matching.OrderByDescending(id);
    }
}
