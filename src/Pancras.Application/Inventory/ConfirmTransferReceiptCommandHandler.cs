using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ConfirmTransferReceiptCommandHandler(
    IInventoryRepository repository,
    ILogger<ConfirmTransferReceiptCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is not null
            && (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, slip.DestinationLocationId)))
        {
            logger.LogWarning("Receipt forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        if (slip is null || slip.Status != TransferStatus.InTransit)
        {
            logger.LogWarning("Receipt rejected for {TransferSlipId}.", command.TransferSlipId);
            return Result<Guid>.Fail(StockError.ReceiptRejected);
        }

        var destinations = new Dictionary<Guid, StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var destination = await repository.FindBalanceForUpdateAsync(itemId, slip.DestinationLocationId, cancellationToken);
            if (destination is null)
            {
                logger.LogWarning("Receipt rejected for {TransferSlipId}.", slip.Id);
                return Result<Guid>.Fail(StockError.ReceiptRejected);
            }

            destinations[itemId] = destination;
        }

        var snapshots = destinations.Values.Select(BalanceSnapshot.Take).ToArray();
        var status = slip.Status;
        if (!slip.TryConfirmReceipt(destinations))
        {
            logger.LogWarning("Receipt rejected for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.ReceiptRejected);
        }

        var occurredAt = DateTimeOffset.UtcNow;
        foreach (var line in slip.Lines)
        {
            await repository.AddMovementAsync(
                StockMovement.Create(line.CatalogItemId, slip.DestinationLocationId, StockMovementKind.Transfer, line.Quantity, slip.Id, occurredAt),
                cancellationToken);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(status);
            logger.LogWarning("Receipt commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.ReceiptRejected);
        }

        logger.LogInformation("Confirmed receipt {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.TransferReceived, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}
