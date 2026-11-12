using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;

namespace Pancras.Domain.Tests;

public class CatalogAccessTests
{
    [Theory]
    [InlineData(EmployeeRole.Admin)]
    [InlineData(EmployeeRole.Director)]
    [InlineData(EmployeeRole.Accountant)]
    public void CanView_AllowsThePriceCatalogRoles(string role)
    {
        Assert.True(CatalogAccess.CanView(role));
    }

    [Theory]
    [InlineData(EmployeeRole.Sales)]
    [InlineData(EmployeeRole.WarehouseKeeper)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead)]
    [InlineData(EmployeeRole.CustomerCare)]
    [InlineData(null)]
    public void CanView_HidesThePriceCatalogFromOtherRoles(string? role)
    {
        Assert.False(CatalogAccess.CanView(role));
    }
}
