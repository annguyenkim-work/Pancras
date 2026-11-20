using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Domain.Tests;

/// <summary>
/// Điều kiện SQL của danh sách phiếu chuyển (<see cref="TransferWorkflowAccess.Visible"/>, <see cref="TransferWorkflowAccess.Waiting"/>)
/// phải khớp từng ô với hàm quyền cùng nguồn (<see cref="InventoryAccess"/>, <see cref="TransferWorkflowAccess.IsWaiting"/>).
/// </summary>
public class TransferWorkflowAccessTests
{
    private static readonly Guid Here = Guid.CreateVersion7();
    private static readonly Guid There = Guid.CreateVersion7();
    private static readonly Guid Other = Guid.CreateVersion7();

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
    public void Visible_MatchesTheViewRule_ForEveryLocationCombination(string? role)
    {
        foreach (var actor in ActorsOf(role))
        {
            var visible = TransferWorkflowAccess.Visible(actor).Compile();
            foreach (var (source, destination) in Routes())
            {
                var expected = InventoryAccess.CanOperate(actor.Role)
                    && (InventoryAccess.CanAccessLocation(actor, source) || InventoryAccess.CanAccessLocation(actor, destination));
                Assert.Equal(expected, visible(SlipOn(source, destination, TransferStatus.Requested)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public void Waiting_MatchesIsWaiting_ForEveryKindStatusAndRoute(string? role)
    {
        foreach (var actor in ActorsOf(role))
        {
            foreach (var kind in Enum.GetValues<TransferWaitingKind>())
            {
                var waiting = TransferWorkflowAccess.Waiting(actor, kind).Compile();
                foreach (var status in Enum.GetValues<TransferStatus>())
                {
                    foreach (var (source, destination) in Routes())
                    {
                        Assert.Equal(
                            TransferWorkflowAccess.IsWaiting(actor, kind, status, source, destination),
                            waiting(SlipOn(source, destination, status)));
                    }
                }
            }
        }
    }

    [Fact]
    public void IsWaiting_FollowsTheWorkflowSteps()
    {
        var finance = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.Accountant, null);
        var keeperHere = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, Here);
        var leadHere = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.ShowroomWarehouseLead, Here);

        Assert.True(TransferWorkflowAccess.IsWaiting(finance, TransferWaitingKind.Decide, TransferStatus.Requested, Here, There));
        Assert.True(TransferWorkflowAccess.IsWaiting(leadHere, TransferWaitingKind.Decide, TransferStatus.Requested, Here, There));
        Assert.False(TransferWorkflowAccess.IsWaiting(keeperHere, TransferWaitingKind.Decide, TransferStatus.Requested, Here, There));
        Assert.True(TransferWorkflowAccess.IsWaiting(keeperHere, TransferWaitingKind.Ship, TransferStatus.Approved, Here, There));
        Assert.False(TransferWorkflowAccess.IsWaiting(keeperHere, TransferWaitingKind.Receive, TransferStatus.InTransit, Here, There));
        Assert.True(TransferWorkflowAccess.IsWaiting(keeperHere, TransferWaitingKind.Receive, TransferStatus.InTransit, There, Here));
        Assert.True(TransferWorkflowAccess.IsWaiting(finance, TransferWaitingKind.Resolve, TransferStatus.ReceivedWithDiscrepancy, Here, There));
        Assert.False(TransferWorkflowAccess.IsWaiting(keeperHere, TransferWaitingKind.Resolve, TransferStatus.ReceivedWithDiscrepancy, Here, There));
        Assert.True(TransferWorkflowAccess.IsWaiting(keeperHere, TransferWaitingKind.Any, TransferStatus.Approved, Here, There));
        Assert.False(TransferWorkflowAccess.IsWaiting(keeperHere, TransferWaitingKind.Any, TransferStatus.Received, Here, There));
    }

    private static IEnumerable<EmployeeActor> ActorsOf(string? role) =>
        new Guid?[] { Here, Other, Guid.Empty, null }.Select(location => new EmployeeActor(Guid.CreateVersion7(), role, location));

    private static IEnumerable<(Guid Source, Guid Destination)> Routes() =>
        [(Here, There), (There, Here), (There, Other), (Other, Here)];

    private static TransferSlip SlipOn(Guid source, Guid destination, TransferStatus status) =>
        TransferSlipFactory.With(source, destination, status);
}

/// <summary>Dựng phiếu ở một trạng thái bất kỳ để thử điều kiện quyền (chỉ dùng trong test: đặt <c>Status</c> bằng reflection).</summary>
internal static class TransferSlipFactory
{
    public static TransferSlip With(Guid source, Guid destination, TransferStatus status)
    {
        var slip = TransferSlip.Create(source, destination, "ghi chú", [(Guid.CreateVersion7(), 1)])!;
        typeof(TransferSlip).GetProperty(nameof(TransferSlip.Status))!.SetValue(slip, status);
        return slip;
    }
}
