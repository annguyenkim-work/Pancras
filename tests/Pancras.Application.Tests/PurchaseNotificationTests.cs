using Pancras.Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Notifications;
using Pancras.Application.Purchasing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

public class PurchaseNotificationTests
{
    private sealed class FakeNoticeStore : INoticeStore
    {
        public List<Notification> Notices { get; } = [];

        public Task AddAsync(Notification notice, CancellationToken cancellationToken)
        {
            Notices.Add(notice);
            return Task.CompletedTask;
        }

        public Task RemoveForDocumentAsync(Guid documentId, CancellationToken cancellationToken)
        {
            Notices.RemoveAll(notice => notice.DocumentId == documentId);
            return Task.CompletedTask;
        }
    }

    private readonly FakePurchaseStore _orders = new();
    private readonly FakeEmployeeRepository _employees = new();
    private readonly FakeNoticeStore _notices = new();
    private readonly Guid _location = Guid.NewGuid();
    private readonly PurchaseOrder _order;
    private readonly Employee _creator = Make("lap", EmployeeRole.Accountant, null);
    private readonly Employee _director = Make("gd", EmployeeRole.Director, null);
    private readonly Employee _keeper;
    private readonly Employee _otherKeeper = Make("kho2", EmployeeRole.WarehouseKeeper, Guid.NewGuid());

    public PurchaseNotificationTests()
    {
        _keeper = Make("kho", EmployeeRole.WarehouseKeeper, _location);
        _employees.Employees.AddRange([_creator, _director, _keeper, _otherKeeper]);
        _order = PurchaseOrder.TryCreate("PO-1", Guid.NewGuid(), _location, "C", null, null, null, 25000m, 0.1m, [(Guid.NewGuid(), 1, 10m, 0.1m, 0m, 0m, 0m)], createdBy: "lap")!;
        _orders.Orders.Add(_order);
    }

    private static Employee Make(string code, string role, Guid? location) =>
        Employee.Create(code, code, code.ToUpperInvariant(), "hash", role, stockLocationId: location);

    private Task<Pancras.Application.Common.Result<Guid, Pancras.Application.Common.PurchaseError>> Run(PurchaseStatusAction action, EmployeeActor actor, string? note = null) =>
        new ChangePurchaseStatusCommandHandler(_orders, _orders, NullLogger<ChangePurchaseStatusCommandHandler>.Instance, notices: _notices, employees: _employees)
            .HandleAsync(new ChangePurchaseStatusCommand(_order.Id, actor, action, note), CancellationToken.None);

    private static EmployeeActor Actor(Employee person) => new(person.Id, person.Role, person.StockLocationId);

    [Fact]
    public async Task Submit_NotifiesFinanceExceptTheSender()
    {
        await Run(PurchaseStatusAction.Submit, Actor(_creator));

        var recipients = _notices.Notices.Select(notice => notice.RecipientEmployeeId).ToArray();
        Assert.Equal([_director.Id], recipients);
        Assert.All(_notices.Notices, notice => Assert.Equal(NotificationKind.Purchase, notice.Kind));
    }

    [Fact]
    public async Task Approve_NotifiesTheCreatorAndTheReceivingLocationStaff_AndReplacesOldNotices()
    {
        await Run(PurchaseStatusAction.Submit, Actor(_creator));
        await Run(PurchaseStatusAction.Approve, Actor(_director));

        var recipients = _notices.Notices.Select(notice => notice.RecipientEmployeeId).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { _creator.Id, _keeper.Id }.OrderBy(id => id).ToArray(), recipients);
        Assert.All(_notices.Notices, notice => Assert.Equal(Notification.PurchaseApprovedStep, notice.Step));
    }

    [Fact]
    public async Task ReturnToDraft_NotifiesTheCreatorWithTheReason()
    {
        await Run(PurchaseStatusAction.Submit, Actor(_creator));
        await Run(PurchaseStatusAction.ReturnToDraft, Actor(_director), "Thiếu giá");

        var notice = Assert.Single(_notices.Notices);
        Assert.Equal(_creator.Id, notice.RecipientEmployeeId);
        Assert.Contains("Thiếu giá", notice.Step, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_NotifiesTheCreatorAndTheReceivingLocationStaff_WithTheReason()
    {
        await Run(PurchaseStatusAction.Cancel, Actor(_director), "Nhà cung cấp hủy hàng");

        var recipients = _notices.Notices.Select(notice => notice.RecipientEmployeeId).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { _creator.Id, _keeper.Id }.OrderBy(id => id).ToArray(), recipients);
        Assert.All(_notices.Notices, notice => Assert.Contains("Nhà cung cấp hủy hàng", notice.Step, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Receipt_NotifiesTheCreatorAndFinance_ExceptTheReceiver()
    {
        await PurchaseNotifier.NotifyReceivedAsync(_notices, _employees, _order, final: true, Actor(_keeper).Id, DateTimeOffset.UtcNow, CancellationToken.None);

        var recipients = _notices.Notices.Select(notice => notice.RecipientEmployeeId).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { _creator.Id, _director.Id }.OrderBy(id => id).ToArray(), recipients);
        Assert.All(_notices.Notices, notice => Assert.Equal(Pancras.Domain.Notifications.Notification.PurchaseReceivedStep, notice.Step));
    }
}
