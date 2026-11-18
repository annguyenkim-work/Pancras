using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

/// <summary>Cấu hình % chiết khấu mặc định của giá đại lý, nhà phân phối, dự án (chỉ phần cấu hình, chưa tính khi bán).</summary>
public class CatalogDiscountPercentHandlerTests
{
    [Fact]
    public async Task Update_SavesThePercentsOfTheThreeTiers()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var command = UpdateCommand(item.Id);
        command.DealerDiscountPercent = 20m;
        command.DistributorDiscountPercent = 12.5m;
        command.ProjectDiscountPercent = 7.25m;

        var result = await UpdateHandler(repository).HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(20m, item.DealerDiscountPercent);
        Assert.Equal(12.5m, item.DistributorDiscountPercent);
        Assert.Equal(7.25m, item.ProjectDiscountPercent);
        Assert.Equal(20m, result.Value!.DealerDiscountPercent);
    }

    [Fact]
    public async Task Update_WithEmptyPercents_ClearsThem()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        item.SetDiscountPercents(10m, 10m, 10m);
        repository.Items.Add(item);

        var result = await UpdateHandler(repository).HandleAsync(UpdateCommand(item.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(item.DealerDiscountPercent);
        Assert.Null(item.DistributorDiscountPercent);
        Assert.Null(item.ProjectDiscountPercent);
    }

    [Theory]
    [InlineData(-1, CatalogError.Negative)]
    [InlineData(100.5, CatalogError.TooLarge)]
    [InlineData(150, CatalogError.TooLarge)]
    public async Task Update_RejectsAPercentOutsideZeroToHundred_AndChangesNothing(double percent, CatalogError expected)
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var command = UpdateCommand(item.Id);
        command.DistributorDiscountPercent = (decimal)percent;

        var result = await UpdateHandler(repository).HandleAsync(command, CancellationToken.None);

        Assert.Equal(expected, result.Error);
        Assert.Contains(result.IssuesOf<CatalogIssue>(), issue => issue.Field == CatalogField.DistributorDiscountPercent);
        Assert.Null(item.DistributorDiscountPercent);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Update_RejectsMoreThanTwoDecimals()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var command = UpdateCommand(item.Id);
        command.DealerDiscountPercent = 12.345m;

        var result = await UpdateHandler(repository).HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.IssuesOf<CatalogIssue>(), issue => issue.Field == CatalogField.DealerDiscountPercent && issue.Kind == CatalogIssueKind.InvalidNumber);
        Assert.Null(item.DealerDiscountPercent);
    }

    [Fact]
    public async Task Update_ByARoleWithoutPriceRights_CannotSetPercents()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var command = UpdateCommand(item.Id);
        command.Role = EmployeeRole.ShowroomWarehouseLead;
        command.DealerDiscountPercent = 20m;

        var result = await UpdateHandler(repository).HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.PriceListForbidden, result.Error);
        Assert.Null(item.DealerDiscountPercent);
    }

    [Fact]
    public async Task Create_SavesThePercents()
    {
        var repository = new FakeCatalogRepository();
        var command = CreateCommand();
        command.DealerDiscountPercent = 20m;
        command.ProjectDiscountPercent = 5m;

        var result = await new CreateCatalogItemCommandHandler(repository, NullLogger<CreateCatalogItemCommandHandler>.Instance)
            .HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(repository.Items);
        Assert.Equal(20m, item.DealerDiscountPercent);
        Assert.Null(item.DistributorDiscountPercent);
        Assert.Equal(5m, item.ProjectDiscountPercent);
    }

    [Fact]
    public async Task Create_RejectsAPercentAboveHundred()
    {
        var repository = new FakeCatalogRepository();
        var command = CreateCommand();
        command.DealerDiscountPercent = 101m;

        var result = await new CreateCatalogItemCommandHandler(repository, NullLogger<CreateCatalogItemCommandHandler>.Instance)
            .HandleAsync(command, CancellationToken.None);

        Assert.Equal(CatalogError.TooLarge, result.Error);
        Assert.Empty(repository.Items);
    }

    private static UpdateCatalogPricesCommandHandler UpdateHandler(FakeCatalogRepository repository) =>
        new(repository, NullLogger<UpdateCatalogPricesCommandHandler>.Instance);

    private static UpdateCatalogPricesCommand UpdateCommand(Guid id) => new()
    {
        Id = id,
        Role = EmployeeRole.Accountant,
        CostPrice = 8000000m,
        RetailPrice = 15000000m,
        DealerPrice = 12000000m,
        DistributorPrice = 11000000m,
        ProjectPrice = 10000000m
    };

    private static CreateCatalogItemCommand CreateCommand() => new()
    {
        Code = "SP77",
        Name = "Sen tắm",
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
