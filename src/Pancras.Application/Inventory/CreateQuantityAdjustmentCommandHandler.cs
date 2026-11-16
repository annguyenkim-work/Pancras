using Pancras.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateQuantityAdjustmentCommandHandler(
    IInventoryRepository repository,
    IEmployeeRepository employees,
    ILogger<CreateQuantityAdjustmentCommandHandler> logger)
{
    public async Task<Result<Guid, StockError>> HandleAsync(CreateQuantityAdjustmentCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, command.StockLocationId))
        {
            logger.LogWarning("Quantity adjustment forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        // Thiếu kho hoặc mặt hàng phải bị từ chối trước khi dựng dòng tồn: StockBalance.Create ném lỗi với mã rỗng.
        if (command.StockLocationId == Guid.Empty || command.CatalogItemId == Guid.Empty)
        {
            return Result<Guid, StockError>.Fail(StockError.MissingTarget);
        }

        if (InventoryRoles.CanPostAdjustment(command.Actor.Role))
        {
            return await PostDirectAsync(command, cancellationToken);
        }

        var before = await repository.FindBalanceForUpdateAsync(command.CatalogItemId, command.StockLocationId, cancellationToken);
        var onHand = before?.OnHand;
        var reserved = before?.Reserved;
        var inTransit = before?.InTransit;
        var decision = QuantityAdjustment.TryCreate(
            command.Actor.Role,
            command.StockLocationId,
            command.CatalogItemId,
            command.Delta,
            command.Reason,
            command.DocumentRef,
            command.ImageRef,
            out var adjustment);
        if (decision != StockDecision.Accepted || adjustment is null)
        {
            logger.LogInformation("Quantity adjustment rejected for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid, StockError>.Fail(StockDecisionErrors.ToError(decision));
        }

        if (before is not null && (before.OnHand != onHand || before.Reserved != reserved || before.InTransit != inTransit))
        {
            before.RestoreUncommitted(onHand ?? 0, reserved ?? 0, inTransit ?? 0);
        }

        await repository.AddAdjustmentAsync(adjustment, cancellationToken);
        await NotifyFinanceAsync(adjustment, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Quantity adjustment commit failed for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid, StockError>.Fail(StockError.MissingEvidence);
        }

        logger.LogInformation("Created quantity adjustment {QuantityAdjustmentId} for {CatalogItemId}.", adjustment.Id, command.CatalogItemId);
        return Result<Guid, StockError>.Ok(adjustment.Id);
    }

    private async Task<Result<Guid, StockError>> PostDirectAsync(CreateQuantityAdjustmentCommand command, CancellationToken cancellationToken)
    {
        var balance = await repository.FindBalanceForUpdateAsync(command.CatalogItemId, command.StockLocationId, cancellationToken);
        var created = false;
        if (balance is null)
        {
            balance = StockBalance.Create(command.CatalogItemId, command.StockLocationId, 0, 0, 0);
            created = true;
        }

        var snapshot = BalanceSnapshot.Take(balance);
        var decision = QuantityAdjustment.TryApplyDirect(
            command.Actor.Role,
            command.StockLocationId,
            command.CatalogItemId,
            command.Delta,
            command.Reason,
            command.DocumentRef,
            command.ImageRef,
            balance,
            out var adjustment);
        if (decision != StockDecision.Accepted || adjustment is null)
        {
            snapshot.Restore();
            logger.LogInformation("Direct quantity adjustment rejected for {CatalogItemId}.", command.CatalogItemId);
            return decision == StockDecision.NegativeStock
                ? Result<Guid, StockError>.Fail(StockError.NegativeStock, StockDecisionErrors.AvailabilityDetail(balance))
                : Result<Guid, StockError>.Fail(StockDecisionErrors.ToError(decision));
        }

        await repository.AddAdjustmentAsync(adjustment, cancellationToken);
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

        if (!await repository.CommitAsync(cancellationToken))
        {
            snapshot.Restore();
            logger.LogWarning("Direct quantity adjustment commit failed for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid, StockError>.Fail(StockError.NegativeStock);
        }

        logger.LogInformation("Posted quantity adjustment {QuantityAdjustmentId}.", adjustment.Id);
        return Result<Guid, StockError>.Ok(adjustment.Id);
    }

    private async Task NotifyFinanceAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken)
    {
        var people = await employees.ListReadOnlyAsync(cancellationToken);
        var occurredAt = DateTimeOffset.UtcNow;
        foreach (var person in people.Where(person => InventoryRoles.CanPostAdjustment(person.Role)))
        {
            await repository.AddNoticeAsync(
                Notification.Create(
                    person.Id,
                    adjustment.StockLocationId,
                    adjustment.Id,
                    NotificationKind.AdjustmentPending,
                    Notification.AdjustmentStep,
                    occurredAt),
                cancellationToken);
        }
    }
}
