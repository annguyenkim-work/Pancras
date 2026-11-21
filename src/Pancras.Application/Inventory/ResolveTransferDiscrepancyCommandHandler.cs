using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Inventory;
using Pancras.Domain.Notifications;

namespace Pancras.Application.Inventory;

/// <summary>Bước cuối: Kế toán/Giám đốc/Admin xác nhận cách xử lý chênh lệch giữa số xuất và số thực nhận (không phân biệt thừa hay thiếu, nhỏ hay lớn).</summary>
public sealed class ResolveTransferDiscrepancyCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<ResolveTransferDiscrepancyCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(ResolveTransferCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        if (!InventoryRoles.IsFinance(command.Actor.Role))
        {
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferNotFound);
        }

        if (string.IsNullOrWhiteSpace(command.Resolution))
        {
            return Result<Guid, StockError>.Fail(StockError.ReasonRequired);
        }

        if (slip.Status != TransferStatus.ReceivedWithDiscrepancy)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        if (!slip.TryResolveDiscrepancy(command.Resolution))
        {
            return Result<Guid, StockError>.Fail(StockError.InvalidText);
        }

        slip.RecordStep(TransferStep.Resolved, (clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Resolved, request, clock, command.Resolution, cancellationToken);
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, Notification.TransferResolvedStep, TransferAudience.Creator | TransferAudience.DestinationStaff, clock, cancellationToken);
        audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferDiscrepancyResolved, "transfer_slip", slip.Id.ToString("D"), null);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Resolved transfer discrepancy {TransferSlipId}.", slip.Id);
        return Result<Guid, StockError>.Ok(slip.Id);
    }
}
