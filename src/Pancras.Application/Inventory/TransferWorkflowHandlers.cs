using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed record DecideTransferCommand(Guid TransferSlipId, EmployeeActor Actor, bool Approve, string? Reason = null);

public sealed record ShipTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? SealNumber = null, string? Carrier = null);

public sealed record ReceivedLineInput(Guid LineId, int Quantity);

public sealed record ResolveTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? Resolution);

public sealed record ReturnTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? Reason);

/// <summary>Bước 1: bộ phận cần hàng lập phiếu đề nghị. Chưa ảnh hưởng tồn kho.</summary>
public sealed class CreateTransferRequestCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateTransferRequestCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid>> HandleAsync(CreateTransferSlipCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role))
        {
            logger.LogWarning("Transfer request forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        // Chọn kho nào cũng được, miễn kho xuất hoặc kho nhận là kho của mình (xin hàng hoặc chuyển hàng đi).
        if (!(InventoryAccess.CanAccessLocation(command.Actor, command.SourceLocationId) || InventoryAccess.CanAccessLocation(command.Actor, command.DestinationLocationId)))
        {
            logger.LogWarning("Transfer request outside own location for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.NotYourLocation);
        }

        var lines = (command.Lines ?? []).Select(line => (line.CatalogItemId, line.Quantity)).ToArray();
        var slip = TransferSlip.Create(command.SourceLocationId, command.DestinationLocationId, command.Note, lines, command.Actor.Id);
        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        if (slip is null || !known.Contains(slip.SourceLocationId) || !known.Contains(slip.DestinationLocationId))
        {
            logger.LogWarning("Transfer request rejected for {StockLocationId}.", command.SourceLocationId);
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        await repository.AddTransferAsync(slip, cancellationToken);
        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Requested, request, clock, slip.Note, cancellationToken);
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, StockNotice.TransferRequestedStep, TransferAudience.Finance | TransferAudience.SourceLead, clock, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        logger.LogInformation("Created transfer request {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferRequested, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}

/// <summary>
/// Bước 2: Trưởng kho xuất (hoặc Admin, Giám đốc, Kế toán) duyệt hoặc từ chối.
/// Duyệt thì kiểm tồn khả dụng và giữ hàng ở kho xuất cho tới khi xuất kho hoặc hủy phiếu.
/// </summary>
public sealed class DecideTransferCommandHandler(
    IInventoryRepository repository,
    ILogger<DecideTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
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

        if (slip.Status != TransferStatus.Requested)
        {
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var snapshots = Array.Empty<BalanceSnapshot>();
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

            snapshots = sources.Values.Select(BalanceSnapshot.Take).ToArray();
            if (!slip.TryApprove(sources))
            {
                foreach (var snapshot in snapshots)
                {
                    snapshot.Restore();
                }

                return Result<Guid>.Fail(StockError.InsufficientStock, await TransferTrail.ShortageAsync(repository, slip, sources, false, cancellationToken));
            }

            slip.RecordStep(TransferStep.Approved, now, request?.ActorCode);
        }
        else if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<Guid>.Fail(StockError.ReasonRequired);
        }
        else if (!slip.TryReject(command.Reason))
        {
            return Result<Guid>.Fail(StockError.InvalidText);
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
            command.Approve ? StockNotice.TransferApprovedStep : StockNotice.TransferRejectedStep,
            command.Approve ? TransferAudience.Creator | TransferAudience.SourceStaff : TransferAudience.Creator,
            clock,
            cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(TransferStatus.Requested);
            return Result<Guid>.Fail(StockError.TransferStateChanged);
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
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
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
            return Result<Guid>.Fail(StockError.TransferStateChanged);
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
                return Result<Guid>.Fail(StockError.InsufficientStock, await TransferTrail.ShortageAsync(repository, slip, sources, true, cancellationToken));
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
        var snapshots = sources.Values.Concat(destinations.Values).Distinct().Select(BalanceSnapshot.Take).ToArray();
        if (!slip.TryConfirmShipment(sources, destinations, command.SealNumber, command.Carrier))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            var shortage = await TransferTrail.ShortageAsync(repository, slip, sources, wasReserved, cancellationToken);
            return shortage.Length > 0
                ? Result<Guid>.Fail(StockError.InsufficientStock, shortage)
                : Result<Guid>.Fail(StockError.InvalidText);
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
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, StockNotice.TransferShippedStep, TransferAudience.Creator | TransferAudience.DestinationStaff, clock, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(TransferStatus.Approved);
            slip.RestoreReserved(wasReserved);
            logger.LogWarning("Shipment commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Shipped transfer {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferShipped, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }

    private static string? SealNote(TransferSlip slip) =>
        string.IsNullOrEmpty(slip.SealNumber) ? null : "Số seal: " + slip.SealNumber;
}

/// <summary>Bước cuối: Kế toán/Giám đốc/Admin xác nhận cách xử lý chênh lệch giữa số xuất và số thực nhận (không phân biệt thừa hay thiếu, nhỏ hay lớn).</summary>
public sealed class ResolveTransferDiscrepancyCommandHandler(
    IInventoryRepository repository,
    ILogger<ResolveTransferDiscrepancyCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
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

        if (slip.Status != TransferStatus.ReceivedWithDiscrepancy)
        {
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        if (!slip.TryResolveDiscrepancy(command.Resolution))
        {
            return Result<Guid>.Fail(StockError.InvalidText);
        }

        slip.RecordStep(TransferStep.Resolved, (clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Resolved, request, clock, command.Resolution, cancellationToken);
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, StockNotice.TransferResolvedStep, TransferAudience.Creator | TransferAudience.DestinationStaff, clock, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            slip.RestoreUncommitted(TransferStatus.ReceivedWithDiscrepancy);
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Resolved transfer discrepancy {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferDiscrepancyResolved, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}

/// <summary>
/// Trả hàng đang vận chuyển về kho xuất (giao nhầm, kho nhận từ chối...). Chỉ Admin, Giám đốc, Kế toán.
/// Phần đang chuyển ở kho nhận bị bỏ, tồn kho xuất được cộng lại đúng số đã xuất. Bắt buộc có lý do.
/// </summary>
public sealed class ReturnTransferCommandHandler(
    IInventoryRepository repository,
    ILogger<ReturnTransferCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid>> HandleAsync(ReturnTransferCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryRoles.IsFinance(command.Actor.Role))
        {
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid>.Fail(StockError.TransferNotFound);
        }

        if (slip.Status != TransferStatus.InTransit)
        {
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<Guid>.Fail(StockError.ReasonRequired);
        }

        var sources = new Dictionary<Guid, StockBalance>();
        var destinations = new Dictionary<Guid, StockBalance>();
        var created = new List<StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var destination = await repository.FindBalanceForUpdateAsync(itemId, slip.DestinationLocationId, cancellationToken);
            if (destination is null)
            {
                return Result<Guid>.Fail(StockError.ReturnRejected);
            }

            destinations[itemId] = destination;
            var source = await repository.FindBalanceForUpdateAsync(itemId, slip.SourceLocationId, cancellationToken);
            if (source is null)
            {
                source = StockBalance.Create(itemId, slip.SourceLocationId, 0, 0, 0);
                created.Add(source);
            }

            sources[itemId] = source;
        }

        var snapshots = sources.Values.Concat(destinations.Values).Distinct().Select(BalanceSnapshot.Take).ToArray();
        if (!slip.TryReturnToSource(sources, destinations, command.Reason))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            return Result<Guid>.Fail(StockError.ReturnRejected);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        foreach (var line in slip.Lines)
        {
            await repository.AddMovementAsync(
                StockMovement.Create(line.CatalogItemId, slip.SourceLocationId, StockMovementKind.Transfer, line.Quantity, slip.Id, now),
                cancellationToken);
        }

        foreach (var balance in created)
        {
            await repository.AddBalanceAsync(balance, cancellationToken);
        }

        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Returned, request, clock, command.Reason, cancellationToken);
        await TransferTrail.NotifyAsync(
            repository,
            employees,
            slip,
            command.Actor.Id,
            StockNotice.TransferReturnedStep,
            TransferAudience.Creator | TransferAudience.SourceStaff | TransferAudience.DestinationStaff,
            clock,
            cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(TransferStatus.InTransit);
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Returned transfer {TransferSlipId} to source.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferReturned, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
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

/// <summary>Sửa phiếu đề nghị khi chưa được duyệt (kho, ghi chú, mặt hàng, số lượng). Chỉ người lập phiếu được sửa.</summary>
public sealed class UpdateTransferRequestCommandHandler(
    IInventoryRepository repository,
    ILogger<UpdateTransferRequestCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid>> HandleAsync(UpdateTransferRequestCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid>.Fail(StockError.TransferNotFound);
        }

        if (!TransferAccess.CanTouch(command.Actor, slip))
        {
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        if (!TransferAccess.IsCreator(command.Actor, slip))
        {
            return Result<Guid>.Fail(StockError.NotCreator);
        }

        if (slip.Status != TransferStatus.Requested)
        {
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        if (!(InventoryAccess.CanAccessLocation(command.Actor, command.SourceLocationId) || InventoryAccess.CanAccessLocation(command.Actor, command.DestinationLocationId)))
        {
            return Result<Guid>.Fail(StockError.NotYourLocation);
        }

        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        var lines = (command.Lines ?? []).Select(line => (line.CatalogItemId, line.Quantity)).ToArray();
        if (!known.Contains(command.SourceLocationId)
            || !known.Contains(command.DestinationLocationId)
            || !slip.TryUpdateRequest(command.SourceLocationId, command.DestinationLocationId, command.Note, lines))
        {
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Edited, request, clock, null, cancellationToken);
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, StockNotice.TransferEditedStep, TransferAudience.Finance | TransferAudience.SourceLead, clock, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            return Result<Guid>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Updated transfer request {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferUpdated, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }
}
