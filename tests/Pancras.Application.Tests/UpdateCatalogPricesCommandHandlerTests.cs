using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class UpdateCatalogPricesCommandHandlerTests
{
    [Theory]
    [InlineData(EmployeeRole.Admin)]
    [InlineData(EmployeeRole.Director)]
    [InlineData(EmployeeRole.Accountant)]
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

    [Theory]
    [InlineData(EmployeeRole.Sales)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead)]
    public async Task OtherRoles_CannotChangeThePriceList(string role)
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);

        var result = await handler.HandleAsync(Command(item.Id, role, 1m), CancellationToken.None);

        Assert.Equal(CatalogError.PriceListForbidden, result.Error);
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
    public async Task Update_KeepsTheImage_UntilANewFileReplacesIt()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        item.SetImage("/uploads/catalog/old.png");
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);
        var command = Command(item.Id, EmployeeRole.Admin, 9000000m);
        command.IncludeDetails = true;
        command.Code = "SP01";
        command.Name = "Vòi";
        command.Category = ProductCategory.SanitaryEquipment;
        command.Unit = "Cái";
        command.HeightMm = 10;
        command.LengthMm = 20;
        command.WidthMm = 30;

        var kept = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(kept.IsSuccess);
        Assert.Equal(10, item.HeightMm);
        Assert.Equal(20, item.LengthMm);
        Assert.Equal(30, item.WidthMm);
        Assert.Equal("/uploads/catalog/old.png", item.ImagePath);

        command.Version = item.Version;
        command.ReplaceImage = true;
        command.ImagePath = "/uploads/catalog/new.png";
        var replaced = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(replaced.IsSuccess);
        Assert.Equal("/uploads/catalog/new.png", item.ImagePath);
        Assert.Equal(10, item.HeightMm);
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

        Assert.Equal(CatalogError.PriceListForbidden, result.Error);
        Assert.Equal("SP01", item.Code);
        Assert.Equal("Vòi", item.Name);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task RetailPrice_AboveTheColumn_IsRejectedOnTheField()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);
        var command = Command(item.Id, EmployeeRole.Admin, 100000000000000000000m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.TooLarge, result.Error);
        Assert.Contains(result.IssuesOf<CatalogIssue>(), issue => issue.Field == CatalogField.RetailPrice && issue.Kind == CatalogIssueKind.TooLarge);
        Assert.Equal(15000000m, item.RetailPrice);
    }

    [Fact]
    public async Task PriceWithinTheColumn_IsStored()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);

        var result = await handler.HandleAsync(Command(item.Id, EmployeeRole.Admin, 1_000_000_000_000_000m), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1_000_000_000_000_000m, item.RetailPrice);
    }

    [Fact]
    public async Task StaleEdit_KeepsTheFirstSave()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);
        var first = Command(item.Id, EmployeeRole.Admin, 300000m);
        first.IncludeDetails = true;
        first.Code = "SP01";
        first.Name = "Tên X";
        first.Category = ProductCategory.SanitaryEquipment;
        first.Unit = "Cái";
        var second = Command(item.Id, EmployeeRole.Director, 260000m);
        second.IncludeDetails = true;
        second.Code = "SP01";
        second.Name = "Tên Y";
        second.Category = ProductCategory.SanitaryEquipment;
        second.Unit = "Cái";

        var saved = await handler.HandleAsync(first, CancellationToken.None);
        var rejected = await handler.HandleAsync(second, CancellationToken.None);

        Assert.True(saved.IsSuccess);
        Assert.Equal(CatalogError.VersionConflict, rejected.Error);
        Assert.Equal("Tên X", item.Name);
        Assert.Equal(300000m, item.RetailPrice);
    }

    [Fact]
    public async Task CostChange_IsRejected_WhenStockIsOnHand()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var stock = new FakeInventoryStore();
        stock.Balances.Add(StockBalance.Create(item.Id, Guid.NewGuid(), 10, 0, 0));
        var handler = new UpdateCatalogPricesCommandHandler(
            repository,
            NullLogger<UpdateCatalogPricesCommandHandler>.Instance,
            stock: stock);
        var command = Command(item.Id, EmployeeRole.Admin, 15000000m);
        command.CostPrice = 111000m;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.CostLocked, result.Error);
        Assert.Equal(8000000m, item.CostPrice);
    }

    [Fact]
    public async Task CostChange_IsAllowed_WhenNothingIsOnHand()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(
            repository,
            NullLogger<UpdateCatalogPricesCommandHandler>.Instance,
            stock: new FakeInventoryStore());
        var command = Command(item.Id, EmployeeRole.Admin, 15000000m);
        command.CostPrice = 111000m;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(111000m, item.CostPrice);
    }

    [Fact]
    public async Task BlankUnit_IsRejectedOnUpdate()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new UpdateCatalogPricesCommandHandler(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);
        var command = Command(item.Id, EmployeeRole.Admin, 15000000m);
        command.IncludeDetails = true;
        command.Code = "SP01";
        command.Name = "Vòi";
        command.Category = ProductCategory.SanitaryEquipment;
        command.Unit = " ";

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.Incomplete, result.Error);
        Assert.Contains(result.IssuesOf<CatalogIssue>(), issue => issue.Field == CatalogField.Unit && issue.Kind == CatalogIssueKind.Missing);
        Assert.Equal("Cái", item.Unit);
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
