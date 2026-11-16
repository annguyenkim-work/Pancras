using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

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
    public async Task<Result<Guid, StockError>> HandleAsync(ReturnTransferCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryRoles.IsFinance(command.Actor.Role))
        {
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferNotFound);
        }

        if (slip.Status != TransferStatus.InTransit)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<Guid, StockError>.Fail(StockError.ReasonRequired);
        }

        var sources = new Dictionary<Guid, StockBalance>();
        var destinations = new Dictionary<Guid, StockBalance>();
        var created = new List<StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var destination = await repository.FindBalanceForUpdateAsync(itemId, slip.DestinationLocationId, cancellationToken);
            if (destination is null)
            {
                return Result<Guid, StockError>.Fail(StockError.ReturnRejected);
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

            return Result<Guid, StockError>.Fail(StockError.ReturnRejected);
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
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Returned transfer {TransferSlipId} to source.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferReturned, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid, StockError>.Ok(slip.Id);
    }
}
