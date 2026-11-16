using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Tests;

public class ListCatalogItemsHandlerTests
{
    [Fact]
    public async Task List_KeepsTheSelectedCategory_AndDropsCombos()
    {
        var repository = new FakeCatalogRepository();
        repository.Items.Add(Item("SP01", "Vòi", ProductCategory.SanitaryEquipment));
        repository.Items.Add(Item("NT01", "Tủ", ProductCategory.BathroomFurniture));
        repository.Items.Add(CatalogItem.CreateCombo("CB01", "Combo vòi", ProductCategory.SanitaryEquipment, "Toto", null, "Bộ", 1m, 1m, null, null, 1m, 1m, 1m, 1m, 1m));
        var handler = new ListCatalogItemsHandler(repository, NullLogger<ListCatalogItemsHandler>.Instance);

        var page = await handler.HandleAsync(
            CatalogListFilter.From(ProductCategoryToken.Sanitary, null),
            CatalogPageRequest.First,
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal("SP01", row.Code);
        Assert.Equal(12m, row.WeightKg);
        Assert.Equal(0.4m, row.VolumeM3);
    }

    [Fact]
    public async Task List_MatchesPartOfTheNameOrBrand()
    {
        var repository = new FakeCatalogRepository();
        repository.Items.Add(Item("SP01", "Vòi lavabo", ProductCategory.SanitaryEquipment, "Toto"));
        repository.Items.Add(Item("SP02", "Bồn cầu", ProductCategory.SanitaryEquipment, "Inax"));
        var handler = new ListCatalogItemsHandler(repository, NullLogger<ListCatalogItemsHandler>.Instance);

        var byName = await handler.HandleAsync(CatalogListFilter.From(null, "lav"), CatalogPageRequest.First, CancellationToken.None);
        var byBrand = await handler.HandleAsync(CatalogListFilter.From(null, "ina"), CatalogPageRequest.First, CancellationToken.None);

        Assert.Equal("SP01", Assert.Single(byName.Rows).Code);
        Assert.Equal("SP02", Assert.Single(byBrand.Rows).Code);
    }

    [Fact]
    public async Task List_UsesTwentyRowsPerPage()
    {
        var repository = new FakeCatalogRepository();
        for (var index = 1; index <= 21; index++)
        {
            repository.Items.Add(Item("SP" + index.ToString("00"), "Hang " + index.ToString("00"), ProductCategory.BuildingMaterials));
        }

        var handler = new ListCatalogItemsHandler(repository, NullLogger<ListCatalogItemsHandler>.Instance);
        var page = await handler.HandleAsync(CatalogListFilter.From(null, null), CatalogPageRequest.First, CancellationToken.None);

        Assert.Equal(20, page.Rows.Count);
        Assert.Equal(21, page.TotalCount);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task Measures_ReturnStoredWeightAndVolume()
    {
        var repository = new FakeCatalogRepository();
        var item = Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new GetCatalogMeasuresHandler(repository, NullLogger<GetCatalogMeasuresHandler>.Instance);

        var measures = await handler.HandleAsync([item.Id, Guid.CreateVersion7()], CancellationToken.None);

        var row = Assert.Single(measures);
        Assert.Equal(item.Id, row.Key);
        Assert.Equal(12m, row.Value.WeightKg);
        Assert.Equal(0.4m, row.Value.VolumeM3);
    }

    internal static CatalogItem Item(
        string code,
        string name,
        ProductCategory category,
        string brand = "Toto") =>
        CatalogItem.Create(code, name, category, brand, "Việt Nam", "Cái", 12m, 0.4m, 6, 1m, 8000000m, 15000000m, 12000000m, 11000000m, 10000000m);
}

public class PageSizesTests
{
    [Theory]
    [InlineData(null, 20)]
    [InlineData(20, 20)]
    [InlineData(50, 50)]
    [InlineData(100, 20)]
    [InlineData(0, 20)]
    public void Normalize_AllowsOnly20Or50(int? size, int expected) =>
        Assert.Equal(expected, Pancras.Application.Common.PageSizes.Normalize(size));
}
