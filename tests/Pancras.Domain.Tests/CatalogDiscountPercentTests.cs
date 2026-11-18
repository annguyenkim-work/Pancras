using Pancras.Domain.Catalog;

namespace Pancras.Domain.Tests;

/// <summary>% chiết khấu mặc định theo mức giá (docs/04-domain/modules/catalog.md, mục "Chiết khấu theo mức giá").</summary>
public class CatalogDiscountPercentTests
{
    [Fact]
    public void NewItem_HasNoDiscountPercents()
    {
        var item = NewItem();

        Assert.Null(item.DealerDiscountPercent);
        Assert.Null(item.DistributorDiscountPercent);
        Assert.Null(item.ProjectDiscountPercent);
    }

    [Fact]
    public void SetDiscountPercents_StoresEachTierSeparately_AndCanClearThem()
    {
        var item = NewItem();

        item.SetDiscountPercents(20m, 12.5m, null);

        Assert.Equal(20m, item.DealerDiscountPercent);
        Assert.Equal(12.5m, item.DistributorDiscountPercent);
        Assert.Null(item.ProjectDiscountPercent);

        item.SetDiscountPercents(null, null, null);

        Assert.Null(item.DealerDiscountPercent);
        Assert.Null(item.DistributorDiscountPercent);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(12.5, true)]
    [InlineData(12.75, true)]
    [InlineData(100, true)]
    [InlineData(-0.01, false)]
    [InlineData(100.01, false)]
    [InlineData(12.755, false)]
    [InlineData(150, false)]
    public void IsValidDiscountPercent_AcceptsZeroToHundredWithAtMostTwoDecimals(double value, bool expected) =>
        Assert.Equal(expected, CatalogItem.IsValidDiscountPercent((decimal)value));

    [Fact]
    public void IsValidDiscountPercent_TreatsEmptyAsValid() =>
        Assert.True(CatalogItem.IsValidDiscountPercent(null));

    private static CatalogItem NewItem() =>
        CatalogItem.Create("SP01", "Vòi", ProductCategory.SanitaryEquipment, "Toto", "Nhật", "Cái", 1m, null, null, null, 1m, 1000000m, 800000m, 700000m, 600000m);
}
