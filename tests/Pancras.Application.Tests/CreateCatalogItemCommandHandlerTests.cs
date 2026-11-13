using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Tests;

public class CreateCatalogItemCommandHandlerTests
{
    [Fact]
    public async Task Create_AddsASanitaryItem_ThatCanBeFoundByCode()
    {
        var repository = new FakeCatalogRepository();
        var handler = Handler(repository);

        var result = await handler.HandleAsync(Command("SP09", "Sen tắm"), CancellationToken.None);
        var page = await new ListCatalogItemsHandler(repository, NullLogger<ListCatalogItemsHandler>.Instance)
            .HandleAsync(CatalogListFilter.From(ProductCategoryToken.Sanitary, "SP09"), CatalogPageRequest.First, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SP09", Assert.Single(page.Rows).Code);
        Assert.Equal(12m, page.Rows[0].WeightKg);
        Assert.Equal(0.4m, page.Rows[0].VolumeM3);
    }

    [Fact]
    public async Task Create_StoresDimensionsAndImagePath()
    {
        var repository = new FakeCatalogRepository();
        var command = Command("SP10", "Chậu");
        command.HeightMm = 800;
        command.LengthMm = 1200;
        command.WidthMm = 450;
        command.ImagePath = "/uploads/catalog/chau.png";

        var result = await Handler(repository).HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(repository.Items);
        Assert.Equal(800, item.HeightMm);
        Assert.Equal(1200, item.LengthMm);
        Assert.Equal(450, item.WidthMm);
        Assert.Equal("/uploads/catalog/chau.png", item.ImagePath);
    }

    [Fact]
    public async Task Create_RejectsANegativeDimension()
    {
        var repository = new FakeCatalogRepository();
        var command = Command("SP11", "Chậu");
        command.WidthMm = -1;

        var result = await Handler(repository).HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.Negative, result.CatalogError);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode_WithoutAddingAnotherRow()
    {
        var repository = new FakeCatalogRepository();
        repository.Items.Add(ListCatalogItemsHandlerTests.Item("sp01", "Vòi", ProductCategory.SanitaryEquipment));
        var handler = Handler(repository);

        var result = await handler.HandleAsync(Command("SP01", "Vòi khác"), CancellationToken.None);

        Assert.Equal(CatalogError.DuplicateCode, result.CatalogError);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task Create_RejectsBlankName()
    {
        var repository = new FakeCatalogRepository();
        var result = await Handler(repository).HandleAsync(Command("SP02", " "), CancellationToken.None);

        Assert.Equal(CatalogError.Incomplete, result.CatalogError);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_RejectsAMissingPrice()
    {
        var repository = new FakeCatalogRepository();
        var command = Command("SP03", "Gương");
        command.ProjectPrice = null;

        var result = await Handler(repository).HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.Incomplete, result.CatalogError);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_RejectsANegativePrice()
    {
        var repository = new FakeCatalogRepository();
        var command = Command("SP04", "Gương");
        command.RetailPrice = -1m;

        var result = await Handler(repository).HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.Negative, result.CatalogError);
        Assert.Empty(repository.Items);
    }

    private static CreateCatalogItemCommandHandler Handler(FakeCatalogRepository repository) =>
        new(repository, NullLogger<CreateCatalogItemCommandHandler>.Instance);

    private static CreateCatalogItemCommand Command(string code, string name) => new()
    {
        Code = code,
        Name = name,
        Category = ProductCategory.SanitaryEquipment,
        Brand = "Toto",
        Unit = "Cái",
        WeightKg = 12m,
        VolumeM3 = 0.4m,
        CostPrice = 8000000m,
        RetailPrice = 15000000m,
        DealerPrice = 12000000m,
        DistributorPrice = 11000000m,
        ProjectPrice = 10000000m
    };
}
