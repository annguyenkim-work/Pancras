using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveStocktakeCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<ApproveStocktakeCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var sheet = await repository.FindStocktakeForUpdateAsync(command.Id, cancellationToken);
        if (sheet is null)
        {
            logger.LogInformation("Stocktake approval rejected for {StocktakeId}.", command.Id);
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        if (!InventoryAccess.CanApprove(command.Actor, sheet.CreatedByRole, sheet.StockLocationId))
        {
            logger.LogWarning("Stocktake approval forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        var decision = sheet.Approve(command.Actor.Role);
        if (decision != StockDecision.Accepted)
        {
            logger.LogInformation("Stocktake approval rejected for {StocktakeId}.", sheet.Id);
            return Result<Guid, StockError>.Fail(decision == StockDecision.NotPending ? StockError.TransferStateChanged : StockError.ApprovalForbidden);
        }

        var applied = await StocktakeApplication.ApplyAsync(
            repository, sheet, (clock ?? TimeProvider.System).GetUtcNow(), cancellationToken);
        if (!applied)
        {
            logger.LogInformation("Stocktake approval would drop stock below the reserved quantity for {StocktakeId}.", sheet.Id);
            return Result<Guid, StockError>.Fail(StockError.NegativeStock);
        }

        await repository.RemoveNoticesAsync(sheet.Id, cancellationToken);
        audit.Stage(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.StocktakeApproved, "stocktake", sheet.Id.ToString("D"), null);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Stocktake approval commit failed for {StocktakeId}.", sheet.Id);
            return Result<Guid, StockError>.Fail(StockError.ApprovalForbidden);
        }

        logger.LogInformation("Approved stocktake {StocktakeId}.", sheet.Id);
        return Result<Guid, StockError>.Ok(sheet.Id);
    }
}
