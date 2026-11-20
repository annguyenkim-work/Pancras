using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Domain.Tests;

/// <summary>
/// Điều kiện SQL "ai thấy đơn nào" (<see cref="PurchaseAccess.Visible"/>) phải khớp từng ô với hàm quyền
/// <see cref="PurchaseAccess.CanView"/>, vì danh sách đơn mua cắt trang trong database dựa vào điều kiện này.
/// </summary>
public class PurchaseVisibilityTests
{
    private static readonly Guid Here = Guid.CreateVersion7();
    private static readonly Guid Elsewhere = Guid.CreateVersion7();

    public static TheoryData<string?> Roles() =>
        [
            EmployeeRole.Admin,
            EmployeeRole.Director,
            EmployeeRole.Accountant,
            EmployeeRole.WarehouseKeeper,
            EmployeeRole.ShowroomWarehouseLead,
            EmployeeRole.Sales,
            "Unknown",
            null
        ];

    [Theory]
    [MemberData(nameof(Roles))]
    public void Visible_MatchesCanView_ForEveryRoleStatusAndLocation(string? role)
    {
        foreach (var actorLocation in new Guid?[] { Here, Elsewhere, null })
        {
            var actor = new EmployeeActor(Guid.CreateVersion7(), role, actorLocation);
            var visible = PurchaseAccess.Visible(actor).Compile();
            foreach (var status in Enum.GetValues<PurchaseStatus>())
            {
                foreach (var orderLocation in new[] { Here, Elsewhere })
                {
                    var order = OrderAt(orderLocation, status);
                    Assert.Equal(PurchaseAccess.CanView(actor, orderLocation, status), visible(order));
                }
            }
        }
    }

    [Fact]
    public void Visible_ForNoActorOrAnEmptyActorId_ShowsNothing()
    {
        var order = OrderAt(Here, PurchaseStatus.Approved);

        Assert.False(PurchaseAccess.Visible(null).Compile()(order));
        Assert.False(PurchaseAccess.Visible(new EmployeeActor(Guid.Empty, EmployeeRole.Admin, null)).Compile()(order));
    }

    private static PurchaseOrder OrderAt(Guid location, PurchaseStatus status)
    {
        var order = PurchaseOrder.TryCreate("PO-V", Guid.CreateVersion7(), location, null, null, null, null, 25000m, 0m, [(Guid.CreateVersion7(), 2, 10m, 0m, 0m, 0m, 0m)])!;
        if (status == PurchaseStatus.Cancelled)
        {
            Assert.True(order.TryCancel("lý do"));
            return order;
        }

        if (status != PurchaseStatus.Draft)
        {
            Assert.True(order.TrySubmit());
        }

        if (status != PurchaseStatus.Draft && status != PurchaseStatus.PendingApproval)
        {
            Assert.True(order.TryApprove());
        }

        if (status == PurchaseStatus.InTransit)
        {
            Assert.True(order.TryMarkInTransit());
        }

        if (status is PurchaseStatus.PartiallyReceived or PurchaseStatus.Received)
        {
            var quantity = status == PurchaseStatus.Received ? 2 : 1;
            Assert.Equal(ReceiptDecision.Ok, order.TryRecordReceipt(order.Lines.ToDictionary(line => line.Id, _ => quantity), status == PurchaseStatus.Received, null, DateTimeOffset.UtcNow, null, out _));
        }

        Assert.Equal(status, order.Status);
        return order;
    }
}
