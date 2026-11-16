using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Suppliers;
using Pancras.Domain.Catalog;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class ListSortTests
{
    private static readonly string[] Allowed = ["name", "code", "country"];

    [Theory]
    [InlineData(null, "name", false)]
    [InlineData("", "name", false)]
    [InlineData("code", "code", false)]
    [InlineData("-country", "country", true)]
    [InlineData("CODE", "code", false)]
    [InlineData("password", "name", false)]
    [InlineData("-password", "name", false)]
    public void Parse_AcceptsOnlyAllowedKeys(string? token, string key, bool descending)
    {
        var sort = ListSort.Parse(token, Allowed, "name");

        Assert.Equal(key, sort.Key);
        Assert.Equal(descending, sort.Descending);
    }

    [Fact]
    public void Toggle_SameKeyFlipsDirection_NewKeyStartsAscending()
    {
        var sort = new ListSort("name", false);

        Assert.Equal("-name", sort.Toggle("name").Token);
        Assert.Equal("name", sort.Toggle("name").Toggle("name").Token);
        Assert.Equal("code", sort.Toggle("code").Token);
    }

    [Fact]
    public void SupplierOrdering_SortsByTheChosenColumn_ThenByCode()
    {
        var suppliers = new[]
        {
            Supplier.Create("b", "Beta", "Việt Nam", "An", "0900000001", "a@b.co", "TK"),
            Supplier.Create("a", "Alpha", "Trung Quốc", "An", "0900000002", "a@b.co", "TK"),
            Supplier.Create("c", "Alpha", "Ý", "An", "0900000003", "a@b.co", "TK")
        }.AsQueryable();

        Assert.Equal(["a", "c", "b"], SupplierOrdering.Apply(suppliers, new ListSort("name", false)).Select(s => s.Code));
        Assert.Equal(["c", "b", "a"], SupplierOrdering.Apply(suppliers, new ListSort("code", true)).Select(s => s.Code));
        Assert.Equal(["c", "b", "a"], SupplierOrdering.Apply(suppliers, new ListSort("country", true)).Select(s => s.Code));
    }

    [Fact]
    public void CatalogOrdering_SortsByTheChosenColumn()
    {
        var items = new[]
        {
            ListCatalogItemsHandlerTests.Item("SP02", "Vòi", ProductCategory.SanitaryEquipment),
            ListCatalogItemsHandlerTests.Item("SP01", "Chậu", ProductCategory.SanitaryEquipment)
        }.AsQueryable();

        Assert.Equal(["SP01", "SP02"], CatalogOrdering.Apply(items, new ListSort("name", false)).Select(i => i.Code));
        Assert.Equal(["SP02", "SP01"], CatalogOrdering.Apply(items, new ListSort("code", true)).Select(i => i.Code));
    }
}
