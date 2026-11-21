using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveQuantityAdjustmentCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<ApproveQuantityAdjustmentCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var adjustment = await repository.FindAdjustmentForUpdateAsync(command.Id, cancellationToken);
        if (adjustment is null)
        {
            logger.LogInformation("Quantity adjustment approval rejected for {QuantityAdjustmentId}.", command.Id);
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        if (!InventoryAccess.CanApproveAdjustment(command.Actor.Role))
        {
            logger.LogWarning("Quantity adjustment approval forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        var balance = await repository.FindBalanceForUpdateAsync(adjustment.CatalogItemId, adjustment.StockLocationId, cancellationToken);
        var created = false;
        if (balance is null)
        {
            balance = StockBalance.Create(adjustment.CatalogItemId, adjustment.StockLocationId, 0, 0, 0);
            created = true;
        }

        var decision = adjustment.Approve(command.Actor.Role, balance);
        if (decision != StockDecision.Accepted)
        {
            logger.LogInformation("Quantity adjustment approval rejected for {QuantityAdjustmentId}.", adjustment.Id);
            return decision switch
            {
                StockDecision.NegativeStock => Result<Guid, StockError>.Fail(StockError.NegativeStock, StockDecisionErrors.AvailabilityDetail(balance)),
                StockDecision.NotPending => Result<Guid, StockError>.Fail(StockError.TransferStateChanged),
                _ => Result<Guid, StockError>.Fail(StockError.ApprovalForbidden)
            };
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
        audit.Stage(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.AdjustmentApproved, "adjustment", adjustment.Id.ToString("D"), null);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Quantity adjustment approval commit failed for {QuantityAdjustmentId}.", adjustment.Id);
            return Result<Guid, StockError>.Fail(StockError.NegativeStock);
        }

        logger.LogInformation("Approved quantity adjustment {QuantityAdjustmentId}.", adjustment.Id);
        return Result<Guid, StockError>.Ok(adjustment.Id);
    }
}
