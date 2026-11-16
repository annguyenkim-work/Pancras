using Pancras.Domain.Suppliers;
using Pancras.Domain.Employees;

namespace Pancras.Domain.Tests;

public class EmployeeAccessTests
{
    private static readonly Guid Kho = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static EmployeeActor Actor(string role, Guid? location = null) => new(Guid.NewGuid(), role, location);

    [Theory]
    [InlineData(EmployeeRole.Admin, true)]
    [InlineData(EmployeeRole.Director, true)]
    [InlineData(EmployeeRole.Accountant, true)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead, true)]
    [InlineData(EmployeeRole.WarehouseKeeper, false)]
    [InlineData(EmployeeRole.Sales, false)]
    [InlineData(EmployeeRole.CustomerCare, false)]
    public void OnlyManagersAndLeadsCanList(string role, bool expected) =>
        Assert.Equal(expected, EmployeeAccess.CanList(Actor(role, Kho)));

    [Fact]
    public void LeadSeesOnlyOwnLocation_OthersSeeAll()
    {
        Assert.Equal(Kho, EmployeeAccess.ListScope(Actor(EmployeeRole.ShowroomWarehouseLead, Kho)));
        Assert.Null(EmployeeAccess.ListScope(Actor(EmployeeRole.Accountant)));
        Assert.True(EmployeeAccess.CanView(Actor(EmployeeRole.ShowroomWarehouseLead, Kho), Guid.NewGuid(), Kho));
        Assert.False(EmployeeAccess.CanView(Actor(EmployeeRole.ShowroomWarehouseLead, Kho), Guid.NewGuid(), Other));
    }

    [Fact]
    public void AnyoneCanViewThemselves()
    {
        var sales = Actor(EmployeeRole.Sales, Kho);
        Assert.True(EmployeeAccess.CanView(sales, sales.Id, Kho));
        Assert.False(EmployeeAccess.CanView(sales, Guid.NewGuid(), Kho));
    }

    [Fact]
    public void CreationRules()
    {
        Assert.True(EmployeeAccess.CanCreate(Actor(EmployeeRole.Admin), EmployeeRole.Admin, null));
        Assert.False(EmployeeAccess.CanCreate(Actor(EmployeeRole.Director), EmployeeRole.Admin, null));
        Assert.True(EmployeeAccess.CanCreate(Actor(EmployeeRole.Director), EmployeeRole.Accountant, null));
        Assert.True(EmployeeAccess.CanCreate(Actor(EmployeeRole.ShowroomWarehouseLead, Kho), EmployeeRole.Sales, Kho));
        Assert.False(EmployeeAccess.CanCreate(Actor(EmployeeRole.ShowroomWarehouseLead, Kho), EmployeeRole.Sales, Other));
        Assert.False(EmployeeAccess.CanCreate(Actor(EmployeeRole.ShowroomWarehouseLead, Kho), EmployeeRole.ShowroomWarehouseLead, Kho));
        Assert.False(EmployeeAccess.CanCreate(Actor(EmployeeRole.Accountant), EmployeeRole.Sales, Kho));
        Assert.False(EmployeeAccess.CanCreate(Actor(EmployeeRole.Admin), "Không tồn tại", null));
    }

    [Fact]
    public void PersonalDataIsAdminOrSelfOnly()
    {
        var director = Actor(EmployeeRole.Director);
        Assert.False(EmployeeAccess.CanEditPersonal(director, Guid.NewGuid()));
        Assert.True(EmployeeAccess.CanEditPersonal(director, director.Id));
        Assert.True(EmployeeAccess.CanEditPersonal(Actor(EmployeeRole.Admin), Guid.NewGuid()));
    }

    [Fact]
    public void AssignmentRules()
    {
        var director = Actor(EmployeeRole.Director);
        Assert.True(EmployeeAccess.CanEditAssignment(director, Guid.NewGuid(), EmployeeRole.Sales));
        Assert.False(EmployeeAccess.CanEditAssignment(director, Guid.NewGuid(), EmployeeRole.Admin));
        Assert.False(EmployeeAccess.CanEditAssignment(director, director.Id, EmployeeRole.Director));
        Assert.True(EmployeeAccess.CanEditAssignment(Actor(EmployeeRole.Admin), Guid.NewGuid(), EmployeeRole.Admin));
        Assert.False(EmployeeAccess.CanEditAssignment(Actor(EmployeeRole.ShowroomWarehouseLead, Kho), Guid.NewGuid(), EmployeeRole.Sales));
        Assert.False(EmployeeAccess.CanAssignRole(director, EmployeeRole.Admin));
        Assert.True(EmployeeAccess.CanAssignRole(Actor(EmployeeRole.Admin), EmployeeRole.Admin));
    }

    [Fact]
    public void OnlyAdminResetsPasswords()
    {
        Assert.True(EmployeeAccess.CanResetPassword(Actor(EmployeeRole.Admin)));
        Assert.False(EmployeeAccess.CanResetPassword(Actor(EmployeeRole.Director)));
    }

    [Theory]
    [InlineData(EmployeeRole.Admin, true)]
    [InlineData(EmployeeRole.Director, true)]
    [InlineData(EmployeeRole.Accountant, true)]
    [InlineData(EmployeeRole.Sales, false)]
    [InlineData(EmployeeRole.WarehouseKeeper, false)]
    [InlineData(EmployeeRole.CustomerCare, false)]
    public void SuppliersAreForManagersOnly(string role, bool expected) =>
        Assert.Equal(expected, SupplierAccess.CanManage(role));

    [Fact]
    public void PasswordPolicy_AcceptsEightThroughOneHundredTwentyEightCharacters()
    {
        Assert.False(PasswordPolicy.IsAcceptable("1234567"));
        Assert.True(PasswordPolicy.IsAcceptable("12345678"));
        Assert.True(PasswordPolicy.IsAcceptable(new string('a', PasswordPolicy.MaxLength)));
        Assert.False(PasswordPolicy.IsAcceptable(new string('a', PasswordPolicy.MaxLength + 1)));
        Assert.False(PasswordPolicy.IsAcceptable(null));
    }
}
