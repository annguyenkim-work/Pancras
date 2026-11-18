using Pancras.Domain.Employees;
using Pancras.Web.Help;

namespace Pancras.Web.Tests;

/// <summary>Nội dung hướng dẫn tích hợp (docs/03-ui-ux/in-app-guide.md): đủ bốn mục cho mỗi màn hình, phân quyền sinh từ hàm Access.</summary>
public class HelpTopicsTests
{
    [Fact]
    public void TheFirstRelease_CoversInventoryTabsAndPurchasing()
    {
        Assert.Equal(
            ["inventory.balances", "inventory.stocktakes", "inventory.transfers", "purchasing"],
            HelpTopics.All.Select(topic => topic.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void EveryTopic_HasObjectiveFeaturesWorkflowAndPermissions()
    {
        foreach (var topic in HelpTopics.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Title), topic.Key);
            Assert.False(string.IsNullOrWhiteSpace(topic.Objective), topic.Key);
            Assert.NotEmpty(topic.Features);
            Assert.True(topic.Workflow.Count >= 2, topic.Key + " cần ít nhất hai bước quy trình.");
            Assert.NotEmpty(topic.Permissions);
            Assert.All(topic.Features.Concat(topic.Workflow), text => Assert.False(string.IsNullOrWhiteSpace(text)));
        }
    }

    [Fact]
    public void Find_ReturnsNullForAnUnknownKey()
    {
        Assert.NotNull(HelpTopics.Find("purchasing"));
        Assert.Null(HelpTopics.Find("khong-co"));
        Assert.Null(HelpTopics.Find(null));
    }

    [Fact]
    public void PermissionGrid_ListsEveryRoleThatCanHaveAccess()
    {
        Assert.Equal(
            [EmployeeRole.Admin, EmployeeRole.Director, EmployeeRole.Accountant, EmployeeRole.ShowroomWarehouseLead, EmployeeRole.WarehouseKeeper, EmployeeRole.Sales],
            HelpRoles.Columns.Select(column => column.Role).ToArray());
    }

    [Fact]
    public void Printing_FollowsTheSourceLocationRule()
    {
        var print = Row("inventory.transfers", "In phiếu xuất kho");

        Assert.True(print.For(EmployeeRole.Admin).Allowed);
        Assert.True(print.For(EmployeeRole.Accountant).Allowed);
        Assert.Null(print.For(EmployeeRole.Admin).Note);
        Assert.True(print.For(EmployeeRole.WarehouseKeeper).Allowed);
        Assert.Contains("kho xuất", print.For(EmployeeRole.WarehouseKeeper).Note, StringComparison.OrdinalIgnoreCase);
        Assert.True(print.For(EmployeeRole.ShowroomWarehouseLead).Allowed);
        Assert.False(print.For(EmployeeRole.Sales).Allowed);
    }

    [Fact]
    public void Cost_IsVisibleToFinanceOnly_InBothModules()
    {
        var stockCost = Row("inventory.balances", "Xem giá vốn");
        var purchaseCost = Row("purchasing", "Xem FOB, thuế và giá vốn");

        foreach (var row in new[] { stockCost, purchaseCost })
        {
            Assert.True(row.For(EmployeeRole.Admin).Allowed);
            Assert.True(row.For(EmployeeRole.Director).Allowed);
            Assert.True(row.For(EmployeeRole.Accountant).Allowed);
            Assert.False(row.For(EmployeeRole.ShowroomWarehouseLead).Allowed);
            Assert.False(row.For(EmployeeRole.WarehouseKeeper).Allowed);
            Assert.False(row.For(EmployeeRole.Sales).Allowed);
        }
    }

    [Fact]
    public void ApprovingATransfer_IsFinanceOrTheLeadOfTheSourceLocation()
    {
        var approve = Row("inventory.transfers", "Duyệt hoặc từ chối phiếu chuyển");

        Assert.True(approve.For(EmployeeRole.Director).Allowed);
        Assert.True(approve.For(EmployeeRole.ShowroomWarehouseLead).Allowed);
        Assert.NotNull(approve.For(EmployeeRole.ShowroomWarehouseLead).Note);
        Assert.False(approve.For(EmployeeRole.WarehouseKeeper).Allowed);
    }

    [Fact]
    public void Receiving_AGoodsOrder_IsManagersOrStaffOfTheReceivingLocation()
    {
        var receive = Row("purchasing", "Nhận hàng (Nhập Kho & Cập Nhật Giá Vốn)");

        Assert.True(receive.For(EmployeeRole.Accountant).Allowed);
        Assert.Null(receive.For(EmployeeRole.Accountant).Note);
        Assert.True(receive.For(EmployeeRole.WarehouseKeeper).Allowed);
        Assert.NotNull(receive.For(EmployeeRole.WarehouseKeeper).Note);
        Assert.False(receive.For(EmployeeRole.Sales).Allowed);
    }

    private static HelpCapability Row(string key, string name) =>
        Assert.Single(HelpTopics.Find(key)!.Permissions, capability => capability.Name == name);
}
