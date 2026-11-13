using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateAndShipTransferCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateAndShipTransferCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(CreateTransferSlipCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, command.SourceLocationId))
        {
            logger.LogWarning("Transfer forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var lines = (command.Lines ?? [])
            .Select(line => (line.CatalogItemId, line.Quantity))
            .ToArray();
        var slip = TransferSlip.Create(command.SourceLocationId, command.DestinationLocationId, command.Note, lines);
        if (slip is null)
        {
            logger.LogWarning("Transfer slip was rejected for {StockLocationId}.", command.SourceLocationId);
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        var knownLocations = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        if (!knownLocations.Contains(slip.SourceLocationId) || !knownLocations.Contains(slip.DestinationLocationId))
        {
            logger.LogWarning("Transfer rejected: unknown or deleted location for {StockLocationId}.", slip.SourceLocationId);
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        var sources = new Dictionary<Guid, StockBalance>();
        var destinations = new Dictionary<Guid, StockBalance>();
        var created = new List<StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var source = await repository.FindBalanceForUpdateAsync(itemId, slip.SourceLocationId, cancellationToken);
            if (source is null)
            {
                logger.LogWarning("Shipment rejected for a new transfer from {StockLocationId}.", slip.SourceLocationId);
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
        if (!slip.TryConfirmShipment(sources, destinations))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            logger.LogWarning("Shipment rejected for a new transfer from {StockLocationId}.", slip.SourceLocationId);
            return Result<Guid>.Fail(StockError.ShipmentRejected);
        }

        await repository.AddTransferAsync(slip, cancellationToken);
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

            logger.LogWarning("Shipment commit failed for a new transfer from {StockLocationId}.", slip.SourceLocationId);
            return Result<Guid>.Fail(StockError.ShipmentRejected);
        }

        logger.LogInformation("Created and shipped transfer {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.TransferShipped, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}
