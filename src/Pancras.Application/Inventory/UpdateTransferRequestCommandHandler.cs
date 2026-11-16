using Pancras.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>Sửa phiếu đề nghị khi chưa được duyệt (kho, ghi chú, mặt hàng, số lượng). Chỉ người lập phiếu được sửa.</summary>
public sealed class UpdateTransferRequestCommandHandler(
    IInventoryRepository repository,
    ILogger<UpdateTransferRequestCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(UpdateTransferRequestCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferNotFound);
        }

        if (!TransferAccess.CanTouch(command.Actor, slip))
        {
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        if (!TransferAccess.IsCreator(command.Actor, slip))
        {
            return Result<Guid, StockError>.Fail(StockError.NotCreator);
        }

        if (slip.Status != TransferStatus.Requested)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        if (!(InventoryAccess.CanAccessLocation(command.Actor, command.SourceLocationId) || InventoryAccess.CanAccessLocation(command.Actor, command.DestinationLocationId)))
        {
            return Result<Guid, StockError>.Fail(StockError.NotYourLocation);
        }

        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        var lines = (command.Lines ?? []).Select(line => (line.CatalogItemId, line.Quantity)).ToArray();
        if (!known.Contains(command.SourceLocationId)
            || !known.Contains(command.DestinationLocationId)
            || !slip.TryUpdateRequest(command.SourceLocationId, command.DestinationLocationId, command.Note, lines))
        {
            return Result<Guid, StockError>.Fail(StockError.InvalidTransfer);
        }

        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Edited, request, clock, null, cancellationToken);
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, Notification.TransferEditedStep, TransferAudience.Finance | TransferAudience.SourceLead, clock, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        logger.LogInformation("Updated transfer request {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferUpdated, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid, StockError>.Ok(slip.Id);
    }
}
