using Pancras.Domain.Notifications;
using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Hủy phiếu chuyển trước khi xuất kho (đang chờ duyệt hoặc đã duyệt). Chỉ người lập phiếu được hủy và phải ghi lý do
/// (<see cref="ConfirmTransferCommand.Note"/>). Phiếu đã duyệt đang giữ hàng thì nhả phần giữ; tồn thực không đổi.
/// Đã xuất hàng bàn giao vận chuyển thì không hủy được nữa (xem <see cref="ReturnTransferCommandHandler"/>).
/// </summary>
public sealed class CancelTransferCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<CancelTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferNotFound);
        }

        if (!TransferAccess.CanTouch(command.Actor, slip))
        {
            logger.LogWarning("Transfer cancel forbidden for employee {EmployeeId}.", command.Actor.Id);
            // Nhật ký từ chối nằm trong giao dịch đang mở nên phải commit để không bị hủy cùng scope.
            audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.AccessDenied, "transfer_slip", command.TransferSlipId.ToString("D"), "Hủy phiếu chuyển");
            await scope.CommitAsync(cancellationToken);
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        if (!TransferAccess.IsCreator(command.Actor, slip))
        {
            logger.LogWarning("Transfer cancel by non-creator employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.NotCreator);
        }

        if (slip.Status is not (TransferStatus.Requested or TransferStatus.Approved))
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        if (string.IsNullOrWhiteSpace(command.Note))
        {
            return Result<Guid, StockError>.Fail(StockError.ReasonRequired);
        }

        var status = slip.Status;
        var wasReserved = slip.StockReserved;
        var sources = new Dictionary<Guid, StockBalance>();
        if (wasReserved)
        {
            foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
            {
                var source = await repository.FindBalanceForUpdateAsync(itemId, slip.SourceLocationId, cancellationToken);
                if (source is not null)
                {
                    sources[itemId] = source;
                }
            }
        }

        if (!slip.TryCancelBeforeShipment(sources))
        {
            logger.LogInformation("Transfer cancel rejected for {TransferSlipId}.", command.TransferSlipId);
            return Result<Guid, StockError>.Fail(StockError.CancelRejected);
        }

        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Cancelled, request, clock, command.Note, cancellationToken);
        await TransferTrail.NotifyAsync(
            repository,
            employees,
            slip,
            command.Actor.Id,
            Notification.TransferCancelledStep,
            status == TransferStatus.Approved ? TransferAudience.SourceStaff : TransferAudience.SourceLead,
            clock,
            cancellationToken);
        audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.TransferCancelled, "transfer_slip", slip.Id.ToString("D"), null);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Cancelled transfer {TransferSlipId}.", slip.Id);
        return Result<Guid, StockError>.Ok(slip.Id);
    }
}
