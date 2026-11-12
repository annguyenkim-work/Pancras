using Pancras.Domain.Catalog;

namespace Pancras.Domain.Tests;

public class CatalogSalePriceTests
{
    [Fact]
    public void Retail_UsesListPrice()
    {
        var item = Sample();

        var resolved = CatalogSalePrice.TryResolve(
            item,
            new SalePriceRequest(SaleCustomerKind.Retail, false, 1m),
            out var price,
            out var error);

        Assert.True(resolved);
        Assert.Equal(SalePriceError.None, error);
        Assert.Equal(item.RetailPrice, price);
    }

    [Fact]
    public void Dealer_UsesDealerPrice()
    {
        var item = Sample();

        var resolved = CatalogSalePrice.TryResolve(
            item,
            new SalePriceRequest(SaleCustomerKind.Dealer, false, null),
            out var price,
            out var error);

        Assert.True(resolved);
        Assert.Equal(item.DealerPrice, price);
        Assert.Equal(SalePriceError.None, error);
    }

    [Fact]
    public void QuoteFile_UsesTheFilePrice()
    {
        var item = Sample();

        var resolved = CatalogSalePrice.TryResolve(
            item,
            new SalePriceRequest(SaleCustomerKind.Retail, true, 9000000.4m),
            out var price,
            out var error);

        Assert.True(resolved);
        Assert.Equal(9000000m, price);
        Assert.Equal(SalePriceError.None, error);
        Assert.NotEqual(item.RetailPrice, price);
    }

    [Theory]
    [InlineData(SaleCustomerKind.Distributor)]
    [InlineData(SaleCustomerKind.Project)]
    public void UndecidedCustomers_DoNotUseACatalogColumn(SaleCustomerKind customer)
    {
        var item = Sample();

        var resolved = CatalogSalePrice.TryResolve(
            item,
            new SalePriceRequest(customer, false, null),
            out var price,
            out var error);

        Assert.False(resolved);
        Assert.Equal(SalePriceError.Undecided, error);
        Assert.Equal(0m, price);
        Assert.NotEqual(item.DistributorPrice, price);
        Assert.NotEqual(item.ProjectPrice, price);
    }

    [Fact]
    public void QuoteFile_StillAppliesWhenTheCustomerKindIsUndecided()
    {
        var item = Sample();

        var resolved = CatalogSalePrice.TryResolve(
            item,
            new SalePriceRequest(SaleCustomerKind.Distributor, true, 7000000m),
            out var price,
            out var error);

        Assert.True(resolved);
        Assert.Equal(7000000m, price);
        Assert.Equal(SalePriceError.None, error);
        Assert.NotEqual(item.DistributorPrice, price);
    }

    private static CatalogItem Sample() =>
        CatalogItem.Create(
            "SP01",
            "Vòi lavabo",
            ProductCategory.SanitaryEquipment,
            "Toto",
            null,
            "Cái",
            12m,
            0.4m,
            null,
            null,
            8000000m,
            15000000m,
            12000000m,
            11000000m,
            10000000m);
}
