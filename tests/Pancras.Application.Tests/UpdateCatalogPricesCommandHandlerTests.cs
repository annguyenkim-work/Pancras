using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

public class UpdateCatalogPricesCommandHandlerTests
{
    [Theory]
    [InlineData(EmployeeRole.Admin)]
    [InlineData(EmployeeRole.Director)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead)]
    public async Task AllowedRole_UpdatesThePrices(string role)
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);

        var result = await handler.HandleAsync(Command(item.Id, role, 9000000m), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(9000000m, item.RetailPrice);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Sales_CannotChangeThePriceList()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);

        var result = await handler.HandleAsync(Command(item.Id, EmployeeRole.Sales, 1m), CancellationToken.None);

        Assert.Equal(CatalogError.PriceListForbidden, result.CatalogError);
        Assert.Equal(15000000m, item.RetailPrice);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AllowedRole_UpdatesTheProductDetails()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);
        var command = Command(item.Id, EmployeeRole.Admin, 9000000m);
        command.IncludeDetails = true;
        command.Code = "SP02";
        command.Name = "Vòi mới";
        command.Category = ProductCategory.BathroomFurniture;
        command.Unit = "Bộ";
        command.WeightKg = 3m;
        command.WarrantyMonths = 24;
        command.MinimumStock = 5m;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SP02", item.Code);
        Assert.Equal("Vòi mới", item.Name);
        Assert.Equal(ProductCategory.BathroomFurniture, item.Category);
        Assert.Equal(3m, item.WeightKg);
        Assert.Equal(9000000m, item.RetailPrice);
    }

    [Fact]
    public async Task Sales_WithDetails_DoesNotChangeTheProduct()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);
        var command = Command(item.Id, EmployeeRole.Sales, 1m);
        command.IncludeDetails = true;
        command.Code = "SP02";
        command.Name = "Khác";
        command.Category = ProductCategory.BathroomFurniture;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.PriceListForbidden, result.CatalogError);
        Assert.Equal("SP01", item.Code);
        Assert.Equal("Vòi", item.Name);
        Assert.Equal(0, repository.SaveCount);
    }

    private static UpdateCatalogPricesCommand Command(Guid id, string role, decimal retail) => new()
    {
        Id = id,
        Role = role,
        CostPrice = 8000000m,
        RetailPrice = retail,
        DealerPrice = 12000000m,
        DistributorPrice = 11000000m,
        ProjectPrice = 10000000m
    };
}
