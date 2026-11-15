using Pancras.Domain.Catalog;

namespace Pancras.Domain.Tests;

public class CatalogItemTests
{
    [Fact]
    public void Create_StoresWeightVolumeAndWholeDongPrices()
    {
        var item = CatalogItem.Create(
            " sp01 ",
            " Vòi lavabo ",
            ProductCategory.SanitaryEquipment,
            " Toto ",
            " Việt Nam ",
            " Cái ",
            12m,
            0.4m,
            12,
            2m,
            8000000.4m,
            15000000m,
            12000000m,
            11000000m,
            10000000m);

        Assert.Equal("sp01", item.Code);
        Assert.Equal("SP01", item.NormalizedCode);
        Assert.Equal("Vòi lavabo", item.Name);
        Assert.Equal(12m, item.WeightKg);
        Assert.Equal(0.4m, item.VolumeM3);
        Assert.Equal(8000000m, item.CostPrice);
        Assert.False(item.IsCombo);
        Assert.True(CatalogItem.SellingPricesIncludeVat);
        Assert.NotEqual(Guid.Empty, item.Id);
    }

    [Fact]
    public void Create_RejectsBlankCode_WithoutReturningAnItem()
    {
        Assert.Throws<ArgumentException>(() => Sample(code: " "));
    }

    [Fact]
    public void Create_RejectsNegativePrice_WithoutReturningAnItem()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample(retail: -1m));
    }

    [Fact]
    public void UpdatePrices_RejectsNegative_AndLeavesTheOldPrices()
    {
        var item = Sample();

        Assert.Throws<ArgumentOutOfRangeException>(() => item.UpdatePrices(1m, -1m, 1m, 1m, 1m));

        Assert.Equal(15000000m, item.RetailPrice);
        Assert.Equal(8000000m, item.CostPrice);
    }

    [Fact]
    public void UpdateDetails_ReplacesTheProfile_AndLeavesThePrices()
    {
        var item = Sample();

        item.UpdateDetails("sp02", " Vòi mới ", ProductCategory.BathroomFurniture, "Inax", "Nhật", "Bộ", 3m, 0.2m, 24, 5m);

        Assert.Equal("sp02", item.Code);
        Assert.Equal("SP02", item.NormalizedCode);
        Assert.Equal("Vòi mới", item.Name);
        Assert.Equal(ProductCategory.BathroomFurniture, item.Category);
        Assert.Equal(3m, item.WeightKg);
        Assert.Equal(15000000m, item.RetailPrice);
    }

    [Fact]
    public void UpdateDetails_RejectsNegativeWeight_AndLeavesTheOldName()
    {
        var item = Sample();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            item.UpdateDetails("SP01", "Khác", ProductCategory.SanitaryEquipment, null, null, null, -1m, null, null, null));

        Assert.Equal("Vòi lavabo", item.Name);
    }

    [Fact]
    public void CreateCombo_IsHiddenFromTheCatalogCreatePath()
    {
        var combo = CatalogItem.CreateCombo(
            "CB01",
            "Combo",
            ProductCategory.SanitaryEquipment,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1m,
            1m,
            1m,
            1m,
            1m);

        Assert.True(combo.IsCombo);
    }

    [Fact]
    public void Create_StoresDimensionsAndImage()
    {
        var item = CatalogItem.Create(
            "SP01",
            "Vòi lavabo",
            ProductCategory.SanitaryEquipment,
            "Toto",
            "Việt Nam",
            "Cái",
            12m,
            0.4m,
            12,
            1m,
            8000000m,
            15000000m,
            12000000m,
            11000000m,
            10000000m,
            800,
            1200,
            450,
            " /uploads/catalog/voi.png ");

        Assert.Equal(800, item.HeightMm);
        Assert.Equal(1200, item.LengthMm);
        Assert.Equal(450, item.WidthMm);
        Assert.Equal("/uploads/catalog/voi.png", item.ImagePath);
    }

    [Fact]
    public void Create_RejectsNegativeHeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogItem.Create(
            "SP01",
            "Vòi",
            ProductCategory.SanitaryEquipment,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1m,
            1m,
            1m,
            1m,
            1m,
            -1,
            null,
            null,
            null));
    }

    private static CatalogItem Sample(string code = "SP01", decimal retail = 15000000m) =>
        CatalogItem.Create(
            code,
            "Vòi lavabo",
            ProductCategory.SanitaryEquipment,
            "Toto",
            "Việt Nam",
            "Cái",
            12m,
            0.4m,
            12,
            1m,
            8000000m,
            retail,
            12000000m,
            11000000m,
            10000000m);
}
