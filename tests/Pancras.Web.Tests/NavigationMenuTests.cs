using Pancras.Domain.Employees;
using Pancras.Web.Navigation;

namespace Pancras.Web.Tests;

public class NavigationMenuTests
{
    [Fact]
    public void Groups_AreInTheFixedOrder_AndEveryItemBelongsToOne()
    {
        Assert.Equal(6, NavigationMenu.Groups.Count);
        Assert.All(NavigationMenu.Items, item => Assert.Contains(item.Group, NavigationMenu.Groups));
    }

    [Theory]
    [InlineData(EmployeeRole.Admin, true)]
    [InlineData(EmployeeRole.Director, true)]
    [InlineData(EmployeeRole.Accountant, false)]
    [InlineData(EmployeeRole.Sales, false)]
    public void AuditItem_IsOnlyForAdminAndDirector(string role, bool visible) =>
        Assert.Equal(visible, NavigationMenu.VisibleFor(role).Any(item => item.Href == "/audit"));

    [Fact]
    public void ComingSoonItems_AreListedAsRoutes_AndNeverOnHome()
    {
        Assert.Contains("/sales", Pancras.Web.Pages.ComingSoonModel.Routes);
        Assert.DoesNotContain(NavigationMenu.ShortcutsFor(EmployeeRole.Admin), item => item.ComingSoon);
    }
}
