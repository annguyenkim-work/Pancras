using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Notifications;

namespace Pancras.Application.Inventory;

/// <summary>
/// Bước 2: Trưởng kho xuất (hoặc Admin, Giám đốc, Kế toán) duyệt hoặc từ chối.
/// Duyệt thì kiểm tồn khả dụng và giữ hàng ở kho xuất cho tới khi xuất kho hoặc hủy phiếu.
/// </summary>
public sealed class DecideTransferCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<DecideTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public static bool CanDecide(EmployeeActor actor, Guid sourceLocationId) =>
        TransferWorkflowAccess.CanDecide(actor, sourceLocationId);

    public async Task<Result<Guid, StockError>> HandleAsync(DecideTransferCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferNotFound);
        }

        if (!CanDecide(command.Actor, slip.SourceLocationId))
        {
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        if (slip.Status != TransferStatus.Requested)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        string? reason = null;
        if (command.Approve)
        {
            var sources = new Dictionary<Guid, StockBalance>();
            foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
            {
                var source = await repository.FindBalanceForUpdateAsync(itemId, slip.SourceLocationId, cancellationToken);
                if (source is not null)
                {
                    sources[itemId] = source;
                }
            }

            if (!slip.TryApprove(sources))
            {
                return Result<Guid, StockError>.Fail(StockError.InsufficientStock, await TransferTrail.ShortageAsync(repository, slip, sources, false, cancellationToken));
            }

            slip.RecordStep(TransferStep.Approved, now, request?.ActorCode);
        }
        else if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<Guid, StockError>.Fail(StockError.ReasonRequired);
        }
        else if (!slip.TryReject(command.Reason))
        {
            return Result<Guid, StockError>.Fail(StockError.InvalidText);
        }
        else
        {
            reason = command.Reason;
        }

        await TransferTrail.RecordAsync(repository, slip, command.Approve ? TransferEventKind.Approved : TransferEventKind.Rejected, request, clock, reason, cancellationToken);
        await TransferTrail.NotifyAsync(
            repository,
            employees,
            slip,
            command.Actor.Id,
            command.Approve ? Notification.TransferApprovedStep : Notification.TransferRejectedStep,
            command.Approve ? TransferAudience.Creator | TransferAudience.SourceStaff : TransferAudience.Creator,
            clock,
            cancellationToken);
        audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, command.Approve ? AuditActions.TransferApproved : AuditActions.TransferRejected, "transfer_slip", slip.Id.ToString("D"), null);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Transfer {TransferSlipId} decided: approve={Approve}.", slip.Id, command.Approve);
        return Result<Guid, StockError>.Ok(slip.Id);
    }
}
