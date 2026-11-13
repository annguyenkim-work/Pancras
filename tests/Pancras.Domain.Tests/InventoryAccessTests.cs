using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Domain.Tests;

public class InventoryAccessTests
{
    private static readonly Guid Kho = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static EmployeeActor Actor(string role, Guid? location = null) => new(Guid.NewGuid(), role, location);

    [Theory]
    [InlineData(EmployeeRole.Admin, true)]
    [InlineData(EmployeeRole.Director, true)]
    [InlineData(EmployeeRole.Accountant, true)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead, true)]
    [InlineData(EmployeeRole.WarehouseKeeper, true)]
    [InlineData(EmployeeRole.Sales, true)]
    [InlineData(EmployeeRole.CustomerCare, false)]
    public void WhoCanViewStock(string role, bool expected) => Assert.Equal(expected, InventoryAccess.CanView(role));

    [Fact]
    public void OnlyFinanceSeesEveryLocationAndCost()
    {
        Assert.Null(InventoryAccess.Scope(Actor(EmployeeRole.Accountant)));
        Assert.Equal(Kho, InventoryAccess.Scope(Actor(EmployeeRole.WarehouseKeeper, Kho)));
        Assert.Equal(Guid.Empty, InventoryAccess.Scope(Actor(EmployeeRole.Sales)));
        Assert.True(InventoryAccess.CanSeeCost(EmployeeRole.Director));
        Assert.False(InventoryAccess.CanSeeCost(EmployeeRole.ShowroomWarehouseLead));
        Assert.False(InventoryAccess.CanSeeCost(EmployeeRole.Sales));
    }

    [Fact]
    public void LocationAccess()
    {
        Assert.True(InventoryAccess.CanAccessLocation(Actor(EmployeeRole.Admin), Other));
        Assert.True(InventoryAccess.CanAccessLocation(Actor(EmployeeRole.WarehouseKeeper, Kho), Kho));
        Assert.False(InventoryAccess.CanAccessLocation(Actor(EmployeeRole.WarehouseKeeper, Kho), Other));
        Assert.False(InventoryAccess.CanAccessLocation(Actor(EmployeeRole.WarehouseKeeper), Kho));
    }

    [Fact]
    public void SalesCanOnlySearch()
    {
        Assert.False(InventoryAccess.CanOperate(EmployeeRole.Sales));
        Assert.False(InventoryAccess.CanExport(EmployeeRole.Sales));
        Assert.True(InventoryAccess.CanOperate(EmployeeRole.WarehouseKeeper));
        Assert.True(InventoryAccess.CanOperate(EmployeeRole.ShowroomWarehouseLead));
    }

    [Fact]
    public void ApprovalRules()
    {
        var lead = Actor(EmployeeRole.ShowroomWarehouseLead, Kho);
        Assert.True(InventoryAccess.CanApprove(lead, EmployeeRole.WarehouseKeeper, Kho));
        Assert.False(InventoryAccess.CanApprove(lead, EmployeeRole.ShowroomWarehouseLead, Kho));
        Assert.False(InventoryAccess.CanApprove(lead, EmployeeRole.WarehouseKeeper, Other));
        Assert.True(InventoryAccess.CanApprove(lead, string.Empty, Kho));
        Assert.True(InventoryAccess.CanApprove(Actor(EmployeeRole.Accountant), EmployeeRole.ShowroomWarehouseLead, Other));
        Assert.False(InventoryAccess.CanApprove(Actor(EmployeeRole.WarehouseKeeper, Kho), EmployeeRole.WarehouseKeeper, Kho));
        Assert.False(InventoryAccess.CanApprove(Actor(EmployeeRole.Sales, Kho), EmployeeRole.WarehouseKeeper, Kho));
        Assert.True(InventoryAccess.CanApproveAdjustment(EmployeeRole.Director));
        Assert.False(InventoryAccess.CanApproveAdjustment(EmployeeRole.ShowroomWarehouseLead));
    }
}
