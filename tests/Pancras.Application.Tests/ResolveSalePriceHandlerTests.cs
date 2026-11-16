using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Tests;

public class ResolveSalePriceHandlerTests
{
    [Fact]
    public async Task RetailDealerAndQuoteFile_UseTheClosedSources()
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new ResolveSalePriceHandler(repository, NullLogger<ResolveSalePriceHandler>.Instance);

        var retail = await handler.HandleAsync(new ResolveSalePriceCommand(item.Id, SaleCustomerKind.Retail, false, null), CancellationToken.None);
        var dealer = await handler.HandleAsync(new ResolveSalePriceCommand(item.Id, SaleCustomerKind.Dealer, false, null), CancellationToken.None);
        var file = await handler.HandleAsync(new ResolveSalePriceCommand(item.Id, SaleCustomerKind.Dealer, true, 5000000m), CancellationToken.None);

        Assert.Equal(15000000m, retail.Value);
        Assert.Equal(12000000m, dealer.Value);
        Assert.Equal(5000000m, file.Value);
    }

    [Theory]
    [InlineData(SaleCustomerKind.Distributor)]
    [InlineData(SaleCustomerKind.Project)]
    public async Task UndecidedCustomers_FailWithoutAPrice(SaleCustomerKind customer)
    {
        var repository = new FakeCatalogRepository();
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        repository.Items.Add(item);
        var handler = new ResolveSalePriceHandler(repository, NullLogger<ResolveSalePriceHandler>.Instance);

        var result = await handler.HandleAsync(new ResolveSalePriceCommand(item.Id, customer, false, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogError.UndecidedSalePrice, result.Error);
        Assert.Equal(0m, result.Value);
    }
}
