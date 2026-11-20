using Pancras.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>Bước 3: thủ kho xuất soạn hàng, niêm phong, ghi số seal và xuất kho. Tồn kho nguồn giảm, kho đích có hàng đang chuyển.</summary>
public sealed class ShipTransferCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<ShipTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(ShipTransferCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferNotFound);
        }

        if (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, slip.SourceLocationId))
        {
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        if (slip.Status != TransferStatus.Approved)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        if (!known.Contains(slip.SourceLocationId) || !known.Contains(slip.DestinationLocationId))
        {
            return Result<Guid, StockError>.Fail(StockError.InvalidTransfer);
        }

        var sources = new Dictionary<Guid, StockBalance>();
        var destinations = new Dictionary<Guid, StockBalance>();
        var created = new List<StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var source = await repository.FindBalanceForUpdateAsync(itemId, slip.SourceLocationId, cancellationToken);
            if (source is null)
            {
                return Result<Guid, StockError>.Fail(StockError.InsufficientStock, await TransferTrail.ShortageAsync(repository, slip, sources, true, cancellationToken));
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

        var wasReserved = slip.StockReserved;
        if (!slip.TryConfirmShipment(sources, destinations, command.SealNumber, command.Carrier))
        {
            var shortage = await TransferTrail.ShortageAsync(repository, slip, sources, wasReserved, cancellationToken);
            return shortage.Length > 0
                ? Result<Guid, StockError>.Fail(StockError.InsufficientStock, shortage)
                : Result<Guid, StockError>.Fail(StockError.InvalidText);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        slip.RecordStep(TransferStep.Shipped, now, request?.ActorCode);
        foreach (var line in slip.Lines)
        {
            await repository.AddMovementAsync(
                StockMovement.Create(line.CatalogItemId, slip.SourceLocationId, StockMovementKind.Transfer, -line.Quantity, slip.Id, now),
                cancellationToken);
        }

        foreach (var balance in created)
        {
            await repository.AddBalanceAsync(balance, cancellationToken);
        }

        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Shipped, request, clock, SealNote(slip), cancellationToken);
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, Notification.TransferShippedStep, TransferAudience.Creator | TransferAudience.DestinationStaff, clock, cancellationToken);
        audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferShipped, "transfer_slip", slip.Id.ToString("D"), null);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Shipment commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Shipped transfer {TransferSlipId}.", slip.Id);
        return Result<Guid, StockError>.Ok(slip.Id);
    }

    private static string? SealNote(TransferSlip slip) =>
        string.IsNullOrEmpty(slip.SealNumber) ? null : "Số seal: " + slip.SealNumber;
}
