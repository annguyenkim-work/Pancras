using Pancras.Domain.Employees;

namespace Pancras.Domain.Tests;

public class EmployeeSecurityStampTests
{
    [Fact]
    public void ReplacePassword_RotatesStamp()
    {
        var employee = Employee.Create("Nguyễn Kim An", "AnNK", "ANNK", "hash", EmployeeRole.Sales);
        var stamp = employee.SecurityStamp;

        Assert.True(employee.ReplacePassword("next-hash"));

        Assert.NotEqual(stamp, employee.SecurityStamp);
    }

    [Fact]
    public void ChangeRole_RotatesStampOnlyWhenTheRoleChanges()
    {
        var employee = Employee.Create("Nguyễn Kim An", "AnNK", "ANNK", "hash", EmployeeRole.Sales);
        var stamp = employee.SecurityStamp;

        Assert.True(employee.ChangeRole(EmployeeRole.Sales));
        Assert.Equal(stamp, employee.SecurityStamp);

        Assert.True(employee.ChangeRole(EmployeeRole.WarehouseKeeper));
        Assert.NotEqual(stamp, employee.SecurityStamp);
        Assert.Equal("AnNK", employee.Code);
    }
}
