using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>Bước 1: bộ phận cần hàng lập phiếu đề nghị. Chưa ảnh hưởng tồn kho.</summary>
public sealed class CreateTransferRequestCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateTransferRequestCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    IEmployeeRepository? employees = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(CreateTransferSlipCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role))
        {
            logger.LogWarning("Transfer request forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        // Chọn kho nào cũng được, miễn kho xuất hoặc kho nhận là kho của mình (xin hàng hoặc chuyển hàng đi).
        if (!(InventoryAccess.CanAccessLocation(command.Actor, command.SourceLocationId) || InventoryAccess.CanAccessLocation(command.Actor, command.DestinationLocationId)))
        {
            logger.LogWarning("Transfer request outside own location for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.NotYourLocation);
        }

        var lines = (command.Lines ?? []).Select(line => (line.CatalogItemId, line.Quantity)).ToArray();
        if (command.RequestKey is Guid key && key != Guid.Empty
            && await repository.FindTransferIdByRequestKeyAsync(key, command.Actor.Id, cancellationToken) is Guid existing)
        {
            logger.LogInformation("Transfer request {RequestKey} was already created as {TransferSlipId}.", key, existing);
            return Result<Guid, StockError>.Ok(existing);
        }

        var requestKey = command.RequestKey is Guid given && given != Guid.Empty ? given : (Guid?)null;
        var slip = TransferSlip.Create(command.SourceLocationId, command.DestinationLocationId, command.Note, lines, command.Actor.Id, requestKey);
        var known = (await repository.ListLocationsAsync(cancellationToken)).Select(location => location.Id).ToHashSet();
        if (slip is null || !known.Contains(slip.SourceLocationId) || !known.Contains(slip.DestinationLocationId))
        {
            logger.LogWarning("Transfer request rejected for {StockLocationId}.", command.SourceLocationId);
            return Result<Guid, StockError>.Fail(StockError.InvalidTransfer);
        }

        await repository.AddTransferAsync(slip, cancellationToken);
        await TransferTrail.RecordAsync(repository, slip, TransferEventKind.Requested, request, clock, slip.Note, cancellationToken);
        await TransferTrail.NotifyAsync(repository, employees, slip, command.Actor.Id, StockNotice.TransferRequestedStep, TransferAudience.Finance | TransferAudience.SourceLead, clock, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            // Hai request cùng mã chạy song song: request thua vi phạm chỉ mục duy nhất, trả lại phiếu của request thắng.
            if (requestKey is Guid lostKey
                && await repository.FindTransferIdByRequestKeyAsync(lostKey, command.Actor.Id, cancellationToken) is Guid winner)
            {
                return Result<Guid, StockError>.Ok(winner);
            }

            return Result<Guid, StockError>.Fail(StockError.InvalidTransfer);
        }

        logger.LogInformation("Created transfer request {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferRequested, "transfer_slip", slip.Id.ToString("D"), null, cancellationToken);
        return Result<Guid, StockError>.Ok(slip.Id);
    }
}
