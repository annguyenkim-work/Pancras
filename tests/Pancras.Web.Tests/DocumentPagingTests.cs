using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;
using Pancras.Web.Formatting;

namespace Pancras.Web.Tests;

/// <summary>Tab Kiểm kê và điều chỉnh gộp ba loại phiếu vào một danh sách phân trang (kiểm kê, điều chỉnh, hủy).</summary>
public class DocumentPagingTests
{
    private static StocktakeRow Stocktake() => new(Guid.CreateVersion7(), "Kho A", "SP", "Vòi", 5, 4, -1, "lý do", "", ApprovalStatus.Pending);

    private static AdjustmentRow Adjustment() => new(Guid.CreateVersion7(), "Kho A", "SP", "Vòi", 1, "lý do", "", ApprovalStatus.Pending);

    private static DisposalRow Disposal() => new(Guid.CreateVersion7(), "Kho A", "SP", "Vòi", "lý do", "", ApprovalStatus.Pending);

    [Fact]
    public void FirstPage_TakesTheFirstDocumentsAcrossAllThreeKinds_InOrder()
    {
        var stocktakes = Enumerable.Range(0, 3).Select(_ => Stocktake()).ToArray();
        var adjustments = Enumerable.Range(0, 3).Select(_ => Adjustment()).ToArray();
        var disposals = Enumerable.Range(0, 3).Select(_ => Disposal()).ToArray();

        var slice = DocumentPaging.Slice(stocktakes, adjustments, disposals, 1, 4, null);

        Assert.Equal(9, slice.Total);
        Assert.Equal(3, slice.Stocktakes.Count);
        Assert.Single(slice.Adjustments);
        Assert.Empty(slice.Disposals);
        Assert.Equal(1, slice.Page);
    }

    [Fact]
    public void LaterPages_ContinueWhereThePreviousOneStopped_AndNothingRepeats()
    {
        var stocktakes = Enumerable.Range(0, 3).Select(_ => Stocktake()).ToArray();
        var adjustments = Enumerable.Range(0, 3).Select(_ => Adjustment()).ToArray();
        var disposals = Enumerable.Range(0, 3).Select(_ => Disposal()).ToArray();
        var seen = new List<Guid>();

        for (var page = 1; page <= 3; page++)
        {
            var slice = DocumentPaging.Slice(stocktakes, adjustments, disposals, page, 4, null);
            seen.AddRange(slice.Stocktakes.Select(row => row.Id));
            seen.AddRange(slice.Adjustments.Select(row => row.Id));
            seen.AddRange(slice.Disposals.Select(row => row.Id));
        }

        Assert.Equal(9, seen.Count);
        Assert.Equal(9, seen.Distinct().Count());
    }

    [Fact]
    public void APageBeyondTheEnd_FallsBackToTheLastPage_AndPageBelowOneToTheFirst()
    {
        var stocktakes = Enumerable.Range(0, 5).Select(_ => Stocktake()).ToArray();

        var beyond = DocumentPaging.Slice(stocktakes, [], [], 99, 2, null);
        var below = DocumentPaging.Slice(stocktakes, [], [], -4, 2, null);

        Assert.Equal(3, beyond.Page);
        Assert.Single(beyond.Stocktakes);
        Assert.Equal(1, below.Page);
        Assert.Equal(2, below.Stocktakes.Count);
    }

    [Fact]
    public void FocusDocument_JumpsToThePageThatHoldsIt()
    {
        var stocktakes = Enumerable.Range(0, 3).Select(_ => Stocktake()).ToArray();
        var adjustments = Enumerable.Range(0, 3).Select(_ => Adjustment()).ToArray();
        var target = adjustments[2];

        var slice = DocumentPaging.Slice(stocktakes, adjustments, [], 1, 2, target.Id);

        Assert.Equal(3, slice.Page);
        Assert.Contains(slice.Adjustments, row => row.Id == target.Id);
    }

    [Fact]
    public void NoDocuments_GivesAnEmptyFirstPage()
    {
        var slice = DocumentPaging.Slice([], [], [], 5, 20, null);

        Assert.Equal(0, slice.Total);
        Assert.Equal(1, slice.Page);
        Assert.Empty(slice.Stocktakes);
    }
}
