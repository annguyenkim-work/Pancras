using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveQuantityAdjustmentCommandHandler(
    IInventoryRepository repository,
    ILogger<ApproveQuantityAdjustmentCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        var adjustment = await repository.FindAdjustmentForUpdateAsync(command.Id, cancellationToken);
        if (adjustment is null)
        {
            logger.LogWarning("Quantity adjustment approval rejected for {QuantityAdjustmentId}.", command.Id);
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        if (!InventoryAccess.CanApproveAdjustment(command.Actor.Role))
        {
            logger.LogWarning("Quantity adjustment approval forbidden for employee {EmployeeId}.", command.Actor.Id);
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
        var decision = adjustment.Approve(command.Actor.Role, balance);
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
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.AdjustmentApproved, "adjustment", adjustment.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(adjustment.Id);
    }
}
