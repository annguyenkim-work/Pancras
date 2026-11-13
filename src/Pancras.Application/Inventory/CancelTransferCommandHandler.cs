using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Hủy phiếu chuyển trước khi xuất kho (đang chờ duyệt hoặc đã duyệt): không đụng tới tồn.
/// Đã xuất hàng bàn giao vận chuyển thì không hủy được nữa.
/// </summary>
public sealed class CancelTransferCommandHandler(
    IInventoryRepository repository,
    ILogger<CancelTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is not null && !TransferAccess.CanTouch(command.Actor, slip))
        {
            logger.LogWarning("Transfer cancel forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var status = slip?.Status ?? TransferStatus.Requested;
        if (slip is null || !slip.TryCancelBeforeShipment())
        {
            logger.LogWarning("Transfer cancel rejected for {TransferSlipId}.", command.TransferSlipId);
            return Result<Guid>.Fail(StockError.CancelRejected);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            slip.RestoreUncommitted(status);
            return Result<Guid>.Fail(StockError.CancelRejected);
        }

        logger.LogInformation("Cancelled transfer {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.TransferCancelled, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}
