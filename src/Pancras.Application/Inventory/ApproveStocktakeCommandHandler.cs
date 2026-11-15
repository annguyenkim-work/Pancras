using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveStocktakeCommandHandler(
    IInventoryRepository repository,
    ILogger<ApproveStocktakeCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        var sheet = await repository.FindStocktakeForUpdateAsync(command.Id, cancellationToken);
        if (sheet is null)
        {
            logger.LogWarning("Stocktake approval rejected for {StocktakeId}.", command.Id);
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        if (!InventoryAccess.CanApprove(command.Actor, sheet.CreatedByRole, sheet.StockLocationId))
        {
            logger.LogWarning("Stocktake approval forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        var status = sheet.Status;
        var decision = sheet.Approve(command.Actor.Role);
        if (decision != StockDecision.Accepted)
        {
            logger.LogWarning("Stocktake approval rejected for {StocktakeId}.", sheet.Id);
            return Result<Guid>.Fail(decision == StockDecision.NotPending ? StockError.TransferStateChanged : StockError.ApprovalForbidden);
        }

        var (applied, snapshot) = await StocktakeApplication.ApplyAsync(
            repository, sheet, (clock ?? TimeProvider.System).GetUtcNow(), cancellationToken);
        if (!applied)
        {
            sheet.RestoreUncommitted(status);
            logger.LogWarning("Stocktake approval would drop stock below the reserved quantity for {StocktakeId}.", sheet.Id);
            return Result<Guid>.Fail(StockError.NegativeStock);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            snapshot?.Restore();
            sheet.RestoreUncommitted(status);
            logger.LogWarning("Stocktake approval commit failed for {StocktakeId}.", sheet.Id);
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        logger.LogInformation("Approved stocktake {StocktakeId}.", sheet.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.StocktakeApproved, "stocktake", sheet.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(sheet.Id);
    }
}
