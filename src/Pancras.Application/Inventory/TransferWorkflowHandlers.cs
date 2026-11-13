using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed record DecideTransferCommand(Guid TransferSlipId, EmployeeActor Actor, bool Approve, string? Reason = null);

public sealed record ShipTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? SealNumber = null, string? Carrier = null);

public sealed record ReceivedLineInput(Guid LineId, int Quantity);

public sealed record ResolveTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? Resolution);

/// <summary>Bước 1: bộ phận cần hàng lập phiếu đề nghị. Chưa ảnh hưởng tồn kho.</summary>
public sealed class CreateTransferRequestCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateTransferRequestCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(CreateTransferSlipCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role)
            || !(InventoryAccess.CanAccessLocation(command.Actor, command.SourceLocationId) || InventoryAccess.CanAccessLocation(command.Actor, command.DestinationLocationId)))
        {
            logger.LogWarning("Transfer request forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var lines = (command.Lines ?? []).Select(line => (line.CatalogItemId, line.Quantity)).ToArray();
        var slip = TransferSlip.Create(command.SourceLocationId, command.DestinationLocationId, command.Note, lines);
        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        if (slip is null || !known.Contains(slip.SourceLocationId) || !known.Contains(slip.DestinationLocationId))
        {
            logger.LogWarning("Transfer request rejected for {StockLocationId}.", command.SourceLocationId);
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        await repository.AddTransferAsync(slip, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        logger.LogInformation("Created transfer request {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferRequested, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}

/// <summary>Bước 2: Trưởng kho xuất (hoặc Admin, Giám đốc, Kế toán) kiểm tồn khả dụng rồi duyệt hoặc từ chối.</summary>
public sealed class DecideTransferCommandHandler(
    IInventoryRepository repository,
    ILogger<DecideTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public static bool CanDecide(EmployeeActor actor, Guid sourceLocationId) =>
        InventoryRoles.IsFinance(actor.Role)
        || (InventoryRoles.IsShowroomLead(actor.Role) && actor.LocationId is Guid own && own == sourceLocationId);

    public async Task<Result<Guid>> HandleAsync(DecideTransferCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid>.Fail(StockError.TransferNotFound);
        }

        if (!CanDecide(command.Actor, slip.SourceLocationId))
        {
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
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
                return Result<Guid>.Fail(slip.Status == TransferStatus.Requested ? StockError.ShipmentRejected : StockError.ApprovalRejected);
            }

            slip.RecordStep(TransferStep.Approved, now, request?.ActorCode);
        }
        else if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<Guid>.Fail(StockError.ReasonRequired);
        }
        else if (!slip.TryReject(command.Reason))
        {
            return Result<Guid>.Fail(StockError.ApprovalRejected);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            return Result<Guid>.Fail(StockError.ApprovalRejected);
        }

        logger.LogInformation("Transfer {TransferSlipId} decided: approve={Approve}.", slip.Id, command.Approve);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, command.Approve ? AuditActions.TransferApproved : AuditActions.TransferRejected, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}

/// <summary>Bước 3: thủ kho xuất soạn hàng, niêm phong, ghi số seal và xuất kho. Tồn kho nguồn giảm, kho đích có hàng đang chuyển.</summary>
public sealed class ShipTransferCommandHandler(
    IInventoryRepository repository,
    ILogger<ShipTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ShipTransferCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid>.Fail(StockError.TransferNotFound);
        }

        if (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, slip.SourceLocationId))
        {
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        if (slip.Status != TransferStatus.Approved)
        {
            return Result<Guid>.Fail(StockError.ShipmentRejected);
        }

        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        if (!known.Contains(slip.SourceLocationId) || !known.Contains(slip.DestinationLocationId))
        {
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
        if (!slip.TryConfirmShipment(sources, destinations, command.SealNumber, command.Carrier))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            return Result<Guid>.Fail(StockError.ShipmentRejected);
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

        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(TransferStatus.Approved);
            logger.LogWarning("Shipment commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.ShipmentRejected);
        }

        logger.LogInformation("Shipped transfer {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferShipped, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}

/// <summary>Bước cuối: Kế toán/Giám đốc/Admin xác nhận cách xử lý chênh lệch giữa số xuất và số thực nhận (không phân biệt thừa hay thiếu, nhỏ hay lớn).</summary>
public sealed class ResolveTransferDiscrepancyCommandHandler(
    IInventoryRepository repository,
    ILogger<ResolveTransferDiscrepancyCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ResolveTransferCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryRoles.IsFinance(command.Actor.Role))
        {
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid>.Fail(StockError.TransferNotFound);
        }

        if (string.IsNullOrWhiteSpace(command.Resolution))
        {
            return Result<Guid>.Fail(StockError.ReasonRequired);
        }

        if (!slip.TryResolveDiscrepancy(command.Resolution))
        {
            return Result<Guid>.Fail(StockError.ApprovalRejected);
        }

        slip.RecordStep(TransferStep.Resolved, (clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
        if (!await repository.CommitAsync(cancellationToken))
        {
            return Result<Guid>.Fail(StockError.ApprovalRejected);
        }

        logger.LogInformation("Resolved transfer discrepancy {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferDiscrepancyResolved, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}

public sealed class UpdateTransferRequestCommand
{
    public Guid TransferSlipId { get; set; }

    public Guid SourceLocationId { get; set; }

    public Guid DestinationLocationId { get; set; }

    public string? Note { get; set; }

    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public IReadOnlyList<TransferLineInput> Lines { get; set; } = [];
}

/// <summary>Sửa phiếu đề nghị khi chưa được duyệt (kho, ghi chú, mặt hàng, số lượng).</summary>
public sealed class UpdateTransferRequestCommandHandler(
    IInventoryRepository repository,
    ILogger<UpdateTransferRequestCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(UpdateTransferRequestCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid>.Fail(StockError.TransferNotFound);
        }

        if (!TransferAccess.CanTouch(command.Actor, slip)
            || !(InventoryAccess.CanAccessLocation(command.Actor, command.SourceLocationId) || InventoryAccess.CanAccessLocation(command.Actor, command.DestinationLocationId)))
        {
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        var lines = (command.Lines ?? []).Select(line => (line.CatalogItemId, line.Quantity)).ToArray();
        if (slip.Status != TransferStatus.Requested
            || !known.Contains(command.SourceLocationId)
            || !known.Contains(command.DestinationLocationId)
            || !slip.TryUpdateRequest(command.SourceLocationId, command.DestinationLocationId, command.Note, lines))
        {
            return Result<Guid>.Fail(slip.Status == TransferStatus.Requested ? StockError.InvalidTransfer : StockError.ApprovalRejected);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        logger.LogInformation("Updated transfer request {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferUpdated, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}
