using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Hủy phiếu chuyển đang trên đường: hoàn hàng về kho nguồn và bỏ phần đang chuyển ở kho đích, trong một transaction.
/// Chỉ người có quyền ở kho nguồn (hoặc Admin, Giám đốc, Kế toán) được hủy; phiếu đã nhận thì không hủy được.
/// </summary>
public sealed class CancelTransferCommandHandler(
    IInventoryRepository repository,
    ILogger<CancelTransferCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is not null
            && (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, slip.SourceLocationId)))
        {
            logger.LogWarning("Transfer cancel forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        if (slip is null || slip.Status != TransferStatus.InTransit)
        {
            logger.LogWarning("Transfer cancel rejected for {TransferSlipId}.", command.TransferSlipId);
            return Result<Guid>.Fail(StockError.CancelRejected);
        }

        var sources = new Dictionary<Guid, StockBalance>();
        var destinations = new Dictionary<Guid, StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var source = await repository.FindBalanceForUpdateAsync(itemId, slip.SourceLocationId, cancellationToken);
            var destination = await repository.FindBalanceForUpdateAsync(itemId, slip.DestinationLocationId, cancellationToken);
            if (source is null || destination is null)
            {
                logger.LogWarning("Transfer cancel rejected for {TransferSlipId}: missing balance.", slip.Id);
                return Result<Guid>.Fail(StockError.CancelRejected);
            }

            sources[itemId] = source;
            destinations[itemId] = destination;
        }

        var snapshots = sources.Values.Concat(destinations.Values).Distinct().Select(BalanceSnapshot.Take).ToArray();
        var status = slip.Status;
        if (!slip.TryCancel(sources, destinations))
        {
            logger.LogWarning("Transfer cancel rejected for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.CancelRejected);
        }

        var occurredAt = DateTimeOffset.UtcNow;
        foreach (var line in slip.Lines)
        {
            await repository.AddMovementAsync(
                StockMovement.Create(line.CatalogItemId, slip.SourceLocationId, StockMovementKind.Transfer, line.Quantity, slip.Id, occurredAt),
                cancellationToken);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(status);
            logger.LogWarning("Transfer cancel commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.CancelRejected);
        }

        logger.LogInformation("Cancelled transfer {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.TransferCancelled, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}
