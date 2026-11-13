using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateTransferSlipCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateTransferSlipCommandHandler> logger)
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

        await repository.AddTransferAsync(slip, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Transfer slip commit failed for {StockLocationId}.", command.SourceLocationId);
            return Result<Guid>.Fail(StockError.InvalidTransfer);
        }

        logger.LogInformation("Created transfer slip {TransferSlipId}.", slip.Id);
        return Result<Guid>.Ok(slip.Id);
    }
}
