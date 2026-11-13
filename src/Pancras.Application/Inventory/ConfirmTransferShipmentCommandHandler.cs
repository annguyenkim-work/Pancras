using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ConfirmTransferShipmentCommandHandler(
    IInventoryRepository repository,
    ILogger<ConfirmTransferShipmentCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is not null
            && (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, slip.SourceLocationId)))
        {
            logger.LogWarning("Shipment forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        if (slip is null || slip.Status != TransferStatus.Draft)
        {
            logger.LogWarning("Shipment rejected for {TransferSlipId}.", command.TransferSlipId);
            return Result<Guid>.Fail(StockError.ShipmentRejected);
        }

        var sources = new Dictionary<Guid, StockBalance>();
        var destinations = new Dictionary<Guid, StockBalance>();
        var created = new List<StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var source = await repository.FindBalanceForUpdateAsync(itemId, slip.SourceLocationId, cancellationToken);
            if (source is null)
            {
                logger.LogWarning("Shipment rejected for {TransferSlipId}.", slip.Id);
                return Result<Guid>.Fail(StockError.ShipmentRejected);
            }

            sources[itemId] = source;
            var destination = await repository.FindBalanceForUpdateAsync(itemId, slip.DestinationLocationId, cancellationToken);
            if (destination is null)
            {
                destination = StockBalance.Create(itemId, slip.DestinationLocationId, 0, 0, 0);
                created.Add(destination);
            }

            destinations[itemId] = destination;
        }

        var snapshots = sources.Values.Concat(destinations.Values).Distinct().Select(BalanceSnapshot.Take).ToArray();
        var status = slip.Status;
        if (!slip.TryConfirmShipment(sources, destinations))
        {
            logger.LogWarning("Shipment rejected for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.ShipmentRejected);
        }

        var occurredAt = DateTimeOffset.UtcNow;
        foreach (var line in slip.Lines)
        {
            await repository.AddMovementAsync(
                StockMovement.Create(line.CatalogItemId, slip.SourceLocationId, StockMovementKind.Transfer, -line.Quantity, slip.Id, occurredAt),
                cancellationToken);
        }

        foreach (var balance in created)
        {
            await repository.AddBalanceAsync(balance, cancellationToken);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(status);
            logger.LogWarning("Shipment commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.ShipmentRejected);
        }

        logger.LogInformation("Confirmed shipment {TransferSlipId}.", slip.Id);
        return Result<Guid>.Ok(slip.Id);
    }
}
