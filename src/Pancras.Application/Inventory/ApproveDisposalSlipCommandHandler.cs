using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveDisposalSlipCommandHandler(
    IInventoryRepository repository,
    IEmployeeRepository employees,
    ILogger<ApproveDisposalSlipCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        var slip = await repository.FindDisposalForUpdateAsync(command.Id, cancellationToken);
        if (slip is null)
        {
            logger.LogWarning("Disposal approval rejected for {DisposalSlipId}.", command.Id);
            return Result<Guid>.Fail(StockError.DisposalRejected);
        }

        var balance = await repository.FindBalanceForUpdateAsync(slip.CatalogItemId, slip.StockLocationId, cancellationToken);
        if (balance is null)
        {
            logger.LogWarning("Disposal approval rejected for {DisposalSlipId}.", slip.Id);
            return Result<Guid>.Fail(slip.Approve(command.Role, StockBalance.Create(slip.CatalogItemId, slip.StockLocationId, 0, 0, 0)) == StockDecision.WrongRole
                ? StockError.ApprovalForbidden
                : StockError.DisposalRejected);
        }

        var snapshot = BalanceSnapshot.Take(balance);
        var status = slip.Status;
        var decision = slip.Approve(command.Role, balance);
        if (decision != StockDecision.Accepted)
        {
            logger.LogWarning("Disposal approval rejected for {DisposalSlipId}.", slip.Id);
            return Result<Guid>.Fail(decision == StockDecision.WrongRole ? StockError.ApprovalForbidden : StockError.DisposalRejected);
        }

        var occurredAt = DateTimeOffset.UtcNow;
        await repository.AddMovementAsync(
            StockMovement.Create(slip.CatalogItemId, slip.StockLocationId, StockMovementKind.Issue, -1, slip.Id, occurredAt),
            cancellationToken);
        await repository.RemoveNoticesAsync(slip.Id, cancellationToken);
        await NotifyWarehouseAsync(slip.StockLocationId, slip.Id, occurredAt, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            snapshot.Restore();
            slip.RestoreUncommitted(status);
            logger.LogWarning("Disposal approval commit failed for {DisposalSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.DisposalRejected);
        }

        logger.LogInformation("Approved disposal slip {DisposalSlipId} at {StockLocationId}.", slip.Id, slip.StockLocationId);
        return Result<Guid>.Ok(slip.Id);
    }

    private async Task NotifyWarehouseAsync(Guid stockLocationId, Guid documentId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var locations = await repository.ListLocationsAsync(cancellationToken);
        var location = locations.FirstOrDefault(item => item.Id == stockLocationId);
        if (location is null)
        {
            return;
        }

        var people = await employees.ListReadOnlyAsync(cancellationToken);
        foreach (var person in people.Where(person => string.Equals(person.Branch.Trim(), location.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await repository.AddNoticeAsync(
                StockNotice.Create(person.Id, stockLocationId, documentId, StockNoticeKind.DisposalApproved, StockNotice.DisposalStep, occurredAt),
                cancellationToken);
        }
    }
}
