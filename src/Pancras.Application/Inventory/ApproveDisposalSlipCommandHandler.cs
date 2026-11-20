using Pancras.Domain.Notifications;
using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveDisposalSlipCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    IEmployeeRepository employees,
    ILogger<ApproveDisposalSlipCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var slip = await repository.FindDisposalForUpdateAsync(command.Id, cancellationToken);
        if (slip is null)
        {
            logger.LogInformation("Disposal approval rejected for {DisposalSlipId}.", command.Id);
            return Result<Guid, StockError>.Fail(StockError.DisposalRejected);
        }

        return await ApplyAsync(slip, command.Actor, isNew: false, scope, cancellationToken);
    }

    /// <summary>
    /// Duyệt một phiếu hủy. Với phiếu mới (<paramref name="isNew"/>), phiếu chưa có trong DB: chỉ khi duyệt được
    /// mới ghi phiếu, phiếu trừ tồn và biến động trong cùng một lần lưu. Duyệt không được thì không để lại gì.
    /// </summary>
    public async Task<Result<Guid, StockError>> ApplyAsync(DisposalSlip slip, EmployeeActor actor, bool isNew, IUnitOfWorkScope scope, CancellationToken cancellationToken)
    {
        var command = new ApproveDocumentCommand(slip.Id, actor);
        if (!InventoryAccess.CanApprove(command.Actor, slip.CreatedByRole, slip.StockLocationId))
        {
            logger.LogWarning("Disposal approval forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        var balance = await repository.FindBalanceForUpdateAsync(slip.CatalogItemId, slip.StockLocationId, cancellationToken);
        if (balance is null)
        {
            logger.LogInformation("Disposal approval rejected for {DisposalSlipId}.", slip.Id);
            return slip.Approve(command.Actor.Role, StockBalance.Create(slip.CatalogItemId, slip.StockLocationId, 0, 0, 0)) == StockDecision.WrongRole
                ? Result<Guid, StockError>.Fail(StockError.ApprovalForbidden)
                : Result<Guid, StockError>.Fail(StockError.DisposalRejected, StockDecisionErrors.AvailabilityDetail(null));
        }

        var decision = slip.Approve(command.Actor.Role, balance);
        if (decision != StockDecision.Accepted)
        {
            logger.LogInformation("Disposal approval rejected for {DisposalSlipId}.", slip.Id);
            return decision switch
            {
                StockDecision.WrongRole => Result<Guid, StockError>.Fail(StockError.ApprovalForbidden),
                StockDecision.NotPending => Result<Guid, StockError>.Fail(StockError.TransferStateChanged),
                _ => Result<Guid, StockError>.Fail(StockError.DisposalRejected, StockDecisionErrors.AvailabilityDetail(balance))
            };
        }

        var occurredAt = DateTimeOffset.UtcNow;
        if (isNew)
        {
            await repository.AddDisposalAsync(slip, cancellationToken);
        }

        await repository.AddMovementAsync(
            StockMovement.Create(slip.CatalogItemId, slip.StockLocationId, StockMovementKind.Issue, -1, slip.Id, occurredAt),
            cancellationToken);
        await repository.RemoveNoticesAsync(slip.Id, cancellationToken);
        await NotifyWarehouseAsync(slip.StockLocationId, slip.Id, occurredAt, cancellationToken);
        audit.Stage(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.DisposalApproved, "disposal", slip.Id.ToString("D"), null);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Disposal approval commit failed for {DisposalSlipId}.", slip.Id);
            return Result<Guid, StockError>.Fail(StockError.DisposalRejected);
        }

        logger.LogInformation("Approved disposal slip {DisposalSlipId} at {StockLocationId}.", slip.Id, slip.StockLocationId);
        return Result<Guid, StockError>.Ok(slip.Id);
    }

    private async Task NotifyWarehouseAsync(Guid stockLocationId, Guid documentId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var locations = await repository.ListLocationsAsync(cancellationToken);
        var location = locations.FirstOrDefault(item => item.Id == stockLocationId);
        if (location is null)
        {
            return;
        }

        var people = await employees.ListReadOnlyAsync(cancellationToken);
        foreach (var person in people.Where(person => person.StockLocationId == stockLocationId && person.IsActive))
        {
            await repository.AddNoticeAsync(
                Notification.Create(person.Id, stockLocationId, documentId, NotificationKind.DisposalApproved, Notification.DisposalStep, occurredAt),
                cancellationToken);
        }
    }
}
