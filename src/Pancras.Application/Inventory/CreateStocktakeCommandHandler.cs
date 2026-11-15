using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateStocktakeCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateStocktakeCommandHandler> logger,
    TimeProvider? clock = null)
{
    public async Task<Result<Guid>> HandleAsync(CreateStocktakeCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, command.StockLocationId))
        {
            logger.LogWarning("Stocktake forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        if (command.CountedQuantity < 0)
        {
            return Result<Guid>.Fail(StockError.InvalidCount);
        }

        var balance = await repository.FindBalanceForUpdateAsync(command.CatalogItemId, command.StockLocationId, cancellationToken);
        var bookQuantity = balance?.OnHand ?? 0;
        var decision = StocktakeSheet.TryCreate(
            command.Actor.Role,
            command.StockLocationId,
            command.CatalogItemId,
            bookQuantity,
            command.CountedQuantity,
            command.Reason,
            command.DocumentRef,
            command.ImageRef,
            out var sheet);
        if (decision != StockDecision.Accepted || sheet is null)
        {
            logger.LogWarning("Stocktake rejected for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid>.Fail(StockDecisionErrors.ToError(decision));
        }

        // Người lập thuộc nhóm tự duyệt (Kế toán, Giám đốc, Admin): biên bản có hiệu lực ngay, áp luôn số đếm vào tồn.
        BalanceSnapshot? snapshot = null;
        if (sheet.Status == ApprovalStatus.Approved)
        {
            var (applied, taken) = await StocktakeApplication.ApplyAsync(
                repository, sheet, (clock ?? TimeProvider.System).GetUtcNow(), cancellationToken);
            if (!applied)
            {
                logger.LogWarning("Stocktake would drop stock below the reserved quantity for {CatalogItemId}.", command.CatalogItemId);
                return Result<Guid>.Fail(StockError.NegativeStock);
            }

            snapshot = taken;
        }

        await repository.AddStocktakeAsync(sheet, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            snapshot?.Restore();
            logger.LogWarning("Stocktake commit failed for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid>.Fail(StockError.MissingEvidence);
        }

        logger.LogInformation("Created stocktake {StocktakeId} for {CatalogItemId}.", sheet.Id, command.CatalogItemId);
        return Result<Guid>.Ok(sheet.Id);
    }
}
