using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

/// <summary>
/// Tab Kiểm kê và điều chỉnh gộp ba loại phiếu vào một danh sách phân trang (kiểm kê, điều chỉnh, hủy). Trang và đoạn cần lấy ở từng
/// bảng được tính từ số đếm, để database chỉ trả phiếu của trang (thay cho cắt trong bộ nhớ trước đây).
/// </summary>
public class StockDocumentWindowTests
{
    [Fact]
    public void FirstPage_TakesTheFirstDocumentsAcrossAllThreeKinds_InOrder()
    {
        var window = StockDocumentWindow.Compute(new StockDocumentCounts(3, 3, 3), 1, 4, null);

        Assert.Equal(1, window.Page);
        Assert.Equal(new StockDocumentRange(0, 3), window.Stocktakes);
        Assert.Equal(new StockDocumentRange(0, 1), window.Adjustments);
        Assert.Equal(0, window.Disposals.Take);
    }

    [Fact]
    public void LaterPages_ContinueWhereThePreviousOneStopped_AndNothingRepeats()
    {
        var counts = new StockDocumentCounts(3, 3, 3);
        var seen = new List<(string Kind, int Index)>();

        for (var page = 1; page <= 3; page++)
        {
            var window = StockDocumentWindow.Compute(counts, page, 4, null);
            seen.AddRange(Enumerable.Range(window.Stocktakes.Skip, window.Stocktakes.Take).Select(index => ("S", index)));
            seen.AddRange(Enumerable.Range(window.Adjustments.Skip, window.Adjustments.Take).Select(index => ("A", index)));
            seen.AddRange(Enumerable.Range(window.Disposals.Skip, window.Disposals.Take).Select(index => ("D", index)));
        }

        Assert.Equal(9, seen.Count);
        Assert.Equal(9, seen.Distinct().Count());
    }

    [Fact]
    public void APageBeyondTheEnd_FallsBackToTheLastPage_AndPageBelowOneToTheFirst()
    {
        var counts = new StockDocumentCounts(5, 0, 0);

        var beyond = StockDocumentWindow.Compute(counts, 99, 2, null);
        var below = StockDocumentWindow.Compute(counts, -4, 2, null);

        Assert.Equal(3, beyond.Page);
        Assert.Equal(new StockDocumentRange(4, 1), beyond.Stocktakes);
        Assert.Equal(1, below.Page);
        Assert.Equal(new StockDocumentRange(0, 2), below.Stocktakes);
    }

    [Fact]
    public void FocusDocument_JumpsToThePageThatHoldsIt()
    {
        // Phiếu điều chỉnh thứ 3 (Index 2) đứng sau 3 biên bản kiểm kê: vị trí 5, trang 3 khi mỗi trang 2 phiếu.
        var window = StockDocumentWindow.Compute(new StockDocumentCounts(3, 3, 0), 1, 2, new StockDocumentPosition(StockDocumentKind.Adjustment, 2));

        Assert.Equal(3, window.Page);
        Assert.Equal(new StockDocumentRange(1, 2), window.Adjustments);
        Assert.Equal(0, window.Stocktakes.Take);
    }

    [Fact]
    public void NoDocuments_GivesAnEmptyFirstPage()
    {
        var window = StockDocumentWindow.Compute(new StockDocumentCounts(0, 0, 0), 5, 20, null);

        Assert.Equal(1, window.Page);
        Assert.Equal(0, window.Stocktakes.Take);
        Assert.Equal(0, window.Adjustments.Take);
        Assert.Equal(0, window.Disposals.Take);
    }
}
