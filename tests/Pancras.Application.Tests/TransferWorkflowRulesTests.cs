using Pancras.Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

/// <summary>Quy tắc sau vòng review: giữ hàng khi duyệt, chỉ người lập sửa/hủy, trả hàng, nhận bắt buộc nhập số, phiếu ghi nhận chênh lệch, lịch sử, thông báo.</summary>
public class TransferWorkflowRulesTests
{
    private readonly FakeInventoryStore _store = new();
    private readonly StockLocation _source = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
    private readonly StockLocation _destination = StockLocation.Create("Showroom B", StockLocationKind.Showroom);
    private readonly Guid _item = Guid.CreateVersion7();
    private readonly StockBalance _sourceBalance;

    public TransferWorkflowRulesTests()
    {
        _store.Locations.Add(_source);
        _store.Locations.Add(_destination);
        _sourceBalance = StockBalance.Create(_item, _source.Id, 10, 0, 0);
        _store.Balances.Add(_sourceBalance);
        _store.Balances.Add(StockBalance.Create(_item, _destination.Id, 0, 0, 0));
    }

    private StockBalance DestinationBalance => _store.Balances.Single(balance => balance.StockLocationId == _destination.Id);

    [Fact]
    public async Task Approve_ReservesTheStock_AndShippingConsumesTheReservation()
    {
        var id = await RequestAsync(TestActors.Keeper(_destination.Id), 4);

        await Decide(id, true);
        var afterApproval = (_sourceBalance.OnHand, _sourceBalance.Reserved, _sourceBalance.Available);
        await Ship(id);

        Assert.Equal((10, 4, 6), afterApproval);
        Assert.Equal(6, _sourceBalance.OnHand);
        Assert.Equal(0, _sourceBalance.Reserved);
        Assert.False(_store.Transfers.Single().StockReserved);
        Assert.Equal(4, DestinationBalance.InTransit);
    }

    [Fact]
    public async Task ReservedStock_CannotBeReservedTwice()
    {
        var first = await RequestAsync(TestActors.Keeper(_destination.Id), 7);
        var second = await RequestAsync(TestActors.Keeper(_destination.Id), 7);

        var ok = await Decide(first, true);
        var shortage = await Decide(second, true);

        Assert.True(ok.IsSuccess);
        Assert.Equal(StockError.InsufficientStock, shortage.Error);
        Assert.Contains("còn 3, cần 7", shortage.Detail);
        Assert.Equal(7, _sourceBalance.Reserved);
        Assert.Equal(TransferStatus.Requested, _store.Transfers.Single(slip => slip.Id == second).Status);
    }

    [Fact]
    public async Task Cancel_ReleasesTheReservation_OfAnApprovedSlip()
    {
        var maker = TestActors.Keeper(_destination.Id);
        var id = await RequestAsync(maker, 4);
        await Decide(id, true);

        var result = await Cancel(id, maker, "Đổi kế hoạch");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _sourceBalance.Reserved);
        Assert.Equal(10, _sourceBalance.OnHand);
        Assert.False(_store.Transfers.Single().StockReserved);
        Assert.Equal(TransferStatus.Cancelled, _store.Transfers.Single().Status);
    }

    [Fact]
    public async Task OnlyTheCreator_CancelsOrEdits_AndCancelNeedsAReason()
    {
        var maker = TestActors.Keeper(_destination.Id);
        var id = await RequestAsync(maker, 2);

        var admin = await Cancel(id, TestActors.Admin, "Hủy giúp");
        var otherKeeper = await Cancel(id, TestActors.Keeper(_destination.Id), "Hủy giúp");
        var outsider = await Cancel(id, TestActors.Keeper(Guid.NewGuid()), "Hủy giúp");
        var noReason = await Cancel(id, maker, " ");
        var edit = await new UpdateTransferRequestCommandHandler(_store, _store, NullLogger<UpdateTransferRequestCommandHandler>.Instance)
            .HandleAsync(
                new UpdateTransferRequestCommand
                {
                    TransferSlipId = id,
                    SourceLocationId = _source.Id,
                    DestinationLocationId = _destination.Id,
                    Actor = TestActors.Admin,
                    Lines = [new TransferLineInput(_item, 3)]
                },
                CancellationToken.None);
        var byCreator = await Cancel(id, maker, "Không cần");

        Assert.Equal(StockError.NotCreator, admin.Error);
        Assert.Equal(StockError.NotCreator, otherKeeper.Error);
        Assert.Equal(StockError.Forbidden, outsider.Error);
        Assert.Equal(StockError.ReasonRequired, noReason.Error);
        Assert.Equal(StockError.NotCreator, edit.Error);
        Assert.True(byCreator.IsSuccess);
    }

    [Fact]
    public async Task Return_IsFinanceOnly_NeedsAReason_AndPutsTheGoodsBackAtTheSource()
    {
        var id = await RequestAsync(TestActors.Keeper(_destination.Id), 4);
        await Decide(id, true);
        await Ship(id);
        var handler = new ReturnTransferCommandHandler(_store, _store, NullLogger<ReturnTransferCommandHandler>.Instance);

        var keeper = await handler.HandleAsync(new ReturnTransferCommand(id, TestActors.Keeper(_source.Id), "Giao nhầm"), CancellationToken.None);
        var noReason = await handler.HandleAsync(new ReturnTransferCommand(id, TestActors.Accountant, " "), CancellationToken.None);
        var ok = await handler.HandleAsync(new ReturnTransferCommand(id, TestActors.Accountant, "Giao nhầm kho"), CancellationToken.None);
        var again = await handler.HandleAsync(new ReturnTransferCommand(id, TestActors.Accountant, "Giao nhầm kho"), CancellationToken.None);

        Assert.Equal(StockError.Forbidden, keeper.Error);
        Assert.Equal(StockError.ReasonRequired, noReason.Error);
        Assert.True(ok.IsSuccess);
        Assert.Equal(StockError.TransferStateChanged, again.Error);
        var slip = _store.Transfers.Single();
        Assert.Equal(TransferStatus.Returned, slip.Status);
        Assert.Equal(10, _sourceBalance.OnHand);
        Assert.Equal(0, DestinationBalance.InTransit);
        Assert.Equal(0, DestinationBalance.OnHand);
        Assert.Contains(_store.Movements, movement => movement.StockLocationId == _source.Id && movement.Quantity == 4 && movement.DocumentId == slip.Id);
        Assert.Equal(TransferEventKind.Returned, _store.TransferEvents.Last().Kind);
        Assert.Equal("Giao nhầm kho", _store.TransferEvents.Last().Note);
    }

    [Fact]
    public async Task Receive_NeedsAQuantityForEveryLine_EvenWhenEverythingArrived()
    {
        var id = await RequestAsync(TestActors.Keeper(_destination.Id), 3);
        await Decide(id, true);
        await Ship(id);

        var none = await Receipt().HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin), CancellationToken.None);
        var empty = await Receipt().HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin, []), CancellationToken.None);
        var ok = await Receipt().HandleAsync(TransferFlow.FullReceipt(_store, id, TestActors.Admin), CancellationToken.None);

        Assert.Equal(StockError.QuantityRequired, none.Error);
        Assert.Equal(StockError.QuantityRequired, empty.Error);
        Assert.True(ok.IsSuccess);
        Assert.Equal(TransferStatus.Received, _store.Transfers.Single().Status);
        Assert.Empty(_store.Adjustments);
    }

    [Theory]
    [InlineData(4, 3, -1)]
    [InlineData(4, 6, 2)]
    public async Task Receive_WithADifference_RecordsAnAdjustment_WithoutTouchingStockAgain(int shipped, int actual, int expectedDelta)
    {
        _sourceBalance.TryAdjust(10);
        var id = await RequestAsync(TestActors.Keeper(_destination.Id), shipped);
        await Decide(id, true);
        await Ship(id);
        var attachments = new FakeTransferAttachments();
        attachments.Attach(id);
        var slip = _store.Transfers.Single();

        var result = await new ConfirmTransferReceiptCommandHandler(_store, _store, NullLogger<ConfirmTransferReceiptCommandHandler>.Instance, attachments: attachments)
            .HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin, [new ReceivedLineInput(slip.Lines[0].Id, actual)], "Đếm lại hai lần"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var record = Assert.Single(_store.Adjustments);
        Assert.Equal(ApprovalStatus.Recorded, record.Status);
        Assert.Equal(expectedDelta, record.Delta);
        Assert.Equal(_destination.Id, record.StockLocationId);
        Assert.Equal(_item, record.CatalogItemId);
        Assert.Equal("Đếm lại hai lần", record.Reason);
        Assert.Equal(StockDocumentCode.ForTransfer(_store.Transfers.Single(slip => slip.Id == id).Number), record.DocumentRef);
        Assert.Equal(id, record.TransferSlipId);
        Assert.Equal(actual, DestinationBalance.OnHand);
        Assert.Equal(0, DestinationBalance.InTransit);
    }

    [Fact]
    public async Task EveryStep_LeavesAHistoryLine_WithTheReason()
    {
        var maker = TestActors.Keeper(_destination.Id);
        var rejected = await RequestAsync(maker, 1);
        await new DecideTransferCommandHandler(_store, _store, NullLogger<DecideTransferCommandHandler>.Instance, request: new RequestStub("AnNK"))
            .HandleAsync(new DecideTransferCommand(rejected, TestActors.Admin, false, "Chưa cần"), CancellationToken.None);
        var shipped = await RequestAsync(maker, 2);
        await Decide(shipped, true);
        await Ship(shipped);
        await Receipt().HandleAsync(TransferFlow.FullReceipt(_store, shipped, TestActors.Admin), CancellationToken.None);

        var rejectedKinds = _store.TransferEvents.Where(item => item.TransferSlipId == rejected).Select(item => item.Kind).ToArray();
        var shippedKinds = _store.TransferEvents.Where(item => item.TransferSlipId == shipped).Select(item => item.Kind).ToArray();

        Assert.Equal([TransferEventKind.Requested, TransferEventKind.Rejected], rejectedKinds);
        Assert.Equal([TransferEventKind.Requested, TransferEventKind.Approved, TransferEventKind.Shipped, TransferEventKind.Received], shippedKinds);
        var rejection = _store.TransferEvents.Single(item => item.Kind == TransferEventKind.Rejected);
        Assert.Equal("Chưa cần", rejection.Note);
        Assert.Equal("AnNK", rejection.ActorCode);
    }

    [Fact]
    public async Task Notifications_GoToApproversWhenRequested_AndToTheCreatorOnEachStep_NeverToTheActor()
    {
        var people = new FakeEmployeeRepository();
        var lead = Employee.Create("Trưởng Kho", "KhoA", "KHOA", "hash", EmployeeRole.ShowroomWarehouseLead, stockLocationId: _source.Id);
        var otherLead = Employee.Create("Trưởng Showroom", "ShowB", "SHOWB", "hash", EmployeeRole.ShowroomWarehouseLead, stockLocationId: _destination.Id);
        var accountant = Employee.Create("Kế Toán", "KeToan", "KETOAN", "hash", EmployeeRole.Accountant);
        var director = Employee.Create("Giám Đốc", "GiamDoc", "GIAMDOC", "hash", EmployeeRole.Director);
        var maker = Employee.Create("Thủ Kho B", "KhoB", "KHOB", "hash", EmployeeRole.WarehouseKeeper, stockLocationId: _destination.Id);
        people.Employees.AddRange([lead, otherLead, accountant, director, maker]);
        var makerActor = new EmployeeActor(maker.Id, maker.Role, _destination.Id);

        var id = (await new CreateTransferRequestCommandHandler(_store, _store, NullLogger<CreateTransferRequestCommandHandler>.Instance, employees: people)
            .HandleAsync(Command(makerActor, 2), CancellationToken.None)).Value;
        var onRequest = _store.Notices.Select(notice => notice.RecipientEmployeeId).ToHashSet();
        var approve = await new DecideTransferCommandHandler(_store, _store, NullLogger<DecideTransferCommandHandler>.Instance, employees: people)
            .HandleAsync(new DecideTransferCommand(id, new EmployeeActor(lead.Id, lead.Role, _source.Id), true), CancellationToken.None);

        Assert.Equal(new HashSet<Guid> { lead.Id, accountant.Id, director.Id }, onRequest);
        Assert.True(approve.IsSuccess);
        var afterApproval = _store.Notices.Where(notice => notice.DocumentId == id).ToArray();
        Assert.All(afterApproval, notice => Assert.Equal(NotificationKind.Transfer, notice.Kind));
        Assert.Contains(afterApproval, notice => notice.RecipientEmployeeId == maker.Id && notice.Step == Notification.TransferApprovedStep);
        Assert.DoesNotContain(afterApproval, notice => notice.RecipientEmployeeId == lead.Id);
        Assert.DoesNotContain(afterApproval, notice => notice.Step == Notification.TransferRequestedStep);
    }

    private async Task<Guid> RequestAsync(EmployeeActor actor, int quantity)
    {
        var result = await TransferFlow.RequestAsync(_store, Command(actor, quantity));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private CreateTransferSlipCommand Command(EmployeeActor actor, int quantity) =>
        new()
        {
            SourceLocationId = _source.Id,
            DestinationLocationId = _destination.Id,
            Actor = actor,
            Lines = [new TransferLineInput(_item, quantity)]
        };

    private Task<Result<Guid, StockError>> Decide(Guid id, bool approve) =>
        new DecideTransferCommandHandler(_store, _store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(id, TestActors.Admin, approve, approve ? null : "Không"), CancellationToken.None);

    private Task<Result<Guid, StockError>> Ship(Guid id) =>
        new ShipTransferCommandHandler(_store, _store, NullLogger<ShipTransferCommandHandler>.Instance)
            .HandleAsync(new ShipTransferCommand(id, TestActors.Admin), CancellationToken.None);

    private Task<Result<Guid, StockError>> Cancel(Guid id, EmployeeActor actor, string? reason) =>
        new CancelTransferCommandHandler(_store, _store, NullLogger<CancelTransferCommandHandler>.Instance)
            .HandleAsync(new ConfirmTransferCommand(id, actor, null, reason), CancellationToken.None);

    private ConfirmTransferReceiptCommandHandler Receipt() =>
        new(_store, _store, NullLogger<ConfirmTransferReceiptCommandHandler>.Instance);

    private sealed class RequestStub(string actorCode) : Pancras.Application.Auditing.IRequestInfo
    {
        public string? IpAddress => null;

        public Guid? ActorId => null;

        public string? ActorCode { get; } = actorCode;
    }
}
