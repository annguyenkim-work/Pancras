using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

/// <summary>Bộ lọc loại phiếu và mã phiếu của tab Kiểm kê và điều chỉnh (docs/04-domain/modules/inventory.md mục 4).</summary>
public class StockDocumentFilterTests
{
    [Fact]
    public void NoFilter_IncludesEveryKind_AndHasNoCodeFragment()
    {
        var filter = new StockDocumentFilter();

        Assert.False(filter.IsActive);
        Assert.All(new[] { StockDocumentKind.Stocktake, StockDocumentKind.Adjustment, StockDocumentKind.Disposal }, kind => Assert.True(filter.Includes(kind)));
        Assert.Null(filter.CodeFragment);
    }

    [Theory]
    [InlineData(StockDocumentKind.Stocktake)]
    [InlineData(StockDocumentKind.Adjustment)]
    [InlineData(StockDocumentKind.Disposal)]
    public void KindFilter_IncludesOnlyThatKind(StockDocumentKind kind)
    {
        var filter = new StockDocumentFilter(kind);

        Assert.True(filter.IsActive);
        foreach (var other in new[] { StockDocumentKind.Stocktake, StockDocumentKind.Adjustment, StockDocumentKind.Disposal })
        {
            Assert.Equal(other == kind, filter.Includes(other));
        }
    }

    [Theory]
    [InlineData("1a2b", "1A2B")]
    [InlineData("  1A2B3C4D ", "1A2B3C4D")]
    [InlineData("KK-1a2b3c4d", "1A2B3C4D")]
    [InlineData("dc-ff", "FF")]
    public void Code_IsCaseInsensitive_AndStripsTheKindPrefix(string typed, string fragment)
    {
        Assert.Equal(fragment, new StockDocumentFilter(null, typed).CodeFragment);
    }

    [Theory]
    [InlineData("KK-1A2B", StockDocumentKind.Stocktake)]
    [InlineData("dc-1A2B", StockDocumentKind.Adjustment)]
    [InlineData("HY-", StockDocumentKind.Disposal)]
    public void CodePrefixWithADash_NarrowsToThatKind(string typed, StockDocumentKind kind)
    {
        var filter = new StockDocumentFilter(null, typed);

        foreach (var other in new[] { StockDocumentKind.Stocktake, StockDocumentKind.Adjustment, StockDocumentKind.Disposal })
        {
            Assert.Equal(other == kind, filter.Includes(other));
        }
    }

    [Fact]
    public void HexLettersThatLookLikeAPrefix_AreAFragment_NotAKind()
    {
        // "DC1A" không có gạch ngang: là đoạn hex của mã, không phải tiền tố Điều chỉnh.
        var filter = new StockDocumentFilter(null, "DC1A");

        Assert.Equal("DC1A", filter.CodeFragment);
        Assert.True(filter.Includes(StockDocumentKind.Stocktake));
    }

    [Theory]
    [InlineData("ZZZ")]
    [InlineData("KK-XYZ")]
    [InlineData("CK-000123")]
    [InlineData("123456789")]
    public void CodeThatCanNeverMatch_IncludesNoKind(string typed)
    {
        var filter = new StockDocumentFilter(null, typed);

        Assert.All(new[] { StockDocumentKind.Stocktake, StockDocumentKind.Adjustment, StockDocumentKind.Disposal }, kind => Assert.False(filter.Includes(kind)));
    }

    [Fact]
    public void KindFilterAndAConflictingPrefix_IncludeNothing()
    {
        var filter = new StockDocumentFilter(StockDocumentKind.Disposal, "KK-1A");

        Assert.All(new[] { StockDocumentKind.Stocktake, StockDocumentKind.Adjustment, StockDocumentKind.Disposal }, kind => Assert.False(filter.Includes(kind)));
    }

    [Theory]
    [InlineData("kk", StockDocumentKind.Stocktake)]
    [InlineData("DC", StockDocumentKind.Adjustment)]
    [InlineData("hy", StockDocumentKind.Disposal)]
    public void KindToken_Parses(string token, StockDocumentKind kind) => Assert.Equal(kind, StockDocumentFilter.ParseKind(token));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void UnknownKindToken_MeansAll(string? token) => Assert.Null(StockDocumentFilter.ParseKind(token));
}
