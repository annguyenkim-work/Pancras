using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveQuantityAdjustmentCommandHandler(
    IInventoryRepository repository,
    ILogger<ApproveQuantityAdjustmentCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        var adjustment = await repository.FindAdjustmentForUpdateAsync(command.Id, cancellationToken);
        if (adjustment is null)
        {
            logger.LogWarning("Quantity adjustment approval rejected for {QuantityAdjustmentId}.", command.Id);
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        var balance = await repository.FindBalanceForUpdateAsync(adjustment.CatalogItemId, adjustment.StockLocationId, cancellationToken);
        var created = false;
        if (balance is null)
        {
            balance = StockBalance.Create(adjustment.CatalogItemId, adjustment.StockLocationId, 0, 0, 0);
            created = true;
        }

        var snapshot = BalanceSnapshot.Take(balance);
        var status = adjustment.Status;
        var decision = adjustment.Approve(command.Role, balance);
        if (decision != StockDecision.Accepted)
        {
            logger.LogWarning("Quantity adjustment approval rejected for {QuantityAdjustmentId}.", adjustment.Id);
            return Result<Guid>.Fail(decision == StockDecision.NegativeStock ? StockError.NegativeStock : StockError.ApprovalForbidden);
        }

        await repository.AddMovementAsync(
            StockMovement.Create(
                adjustment.CatalogItemId,
                adjustment.StockLocationId,
                StockMovementKind.Adjustment,
                adjustment.Delta,
                adjustment.Id,
                DateTimeOffset.UtcNow),
            cancellationToken);
        if (created)
        {
            await repository.AddBalanceAsync(balance, cancellationToken);
        }

        await repository.RemoveNoticesAsync(adjustment.Id, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            snapshot.Restore();
            adjustment.RestoreUncommitted(status);
            logger.LogWarning("Quantity adjustment approval commit failed for {QuantityAdjustmentId}.", adjustment.Id);
            return Result<Guid>.Fail(StockError.NegativeStock);
        }

        logger.LogInformation("Approved quantity adjustment {QuantityAdjustmentId}.", adjustment.Id);
        return Result<Guid>.Ok(adjustment.Id);
    }
}
