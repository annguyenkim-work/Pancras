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

public class EmployeeOrderingTests
{
    private static Pancras.Application.Employees.EmployeeCard Card(string name, string code, string role, string location) =>
        new(Guid.NewGuid(), name, code, role, string.Empty, string.Empty, null, location, string.Empty, string.Empty, string.Empty, true);

    private static readonly Pancras.Application.Employees.EmployeeCard[] Cards =
    [
        Card("Bình", "b01", "Kế toán", "Kho 2"),
        Card("An", "c02", "Admin", "Kho 1"),
        Card("An", "a03", "Thủ kho", "Kho 3")
    ];

    [Fact]
    public void Apply_SortsByTheChosenColumn_ThenByCode()
    {
        Assert.Equal(["a03", "c02", "b01"], Pancras.Application.Employees.EmployeeOrdering.Apply(Cards, new ListSort("name", false)).Select(c => c.Code));
        Assert.Equal(["c02", "b01", "a03"], Pancras.Application.Employees.EmployeeOrdering.Apply(Cards, new ListSort("code", true)).Select(c => c.Code));
        Assert.Equal(["c02", "b01", "a03"], Pancras.Application.Employees.EmployeeOrdering.Apply(Cards, new ListSort("role", false)).Select(c => c.Code));
        Assert.Equal(["a03", "b01", "c02"], Pancras.Application.Employees.EmployeeOrdering.Apply(Cards, new ListSort("location", true)).Select(c => c.Code));
    }

    [Fact]
    public void Parse_FallsBackToName()
    {
        Assert.Equal("name", Pancras.Application.Employees.EmployeeOrdering.Parse("password").Key);
    }
}
