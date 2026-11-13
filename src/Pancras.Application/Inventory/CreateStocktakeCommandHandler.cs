using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateStocktakeCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateStocktakeCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(CreateStocktakeCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, command.StockLocationId))
        {
            logger.LogWarning("Stocktake forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
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
            return Result<Guid>.Fail(Map(decision));
        }

        await repository.AddStocktakeAsync(sheet, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Stocktake commit failed for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid>.Fail(StockError.MissingEvidence);
        }

        logger.LogInformation("Created stocktake {StocktakeId} for {CatalogItemId}.", sheet.Id, command.CatalogItemId);
        return Result<Guid>.Ok(sheet.Id);
    }

    private static StockError Map(StockDecision decision) => decision switch
    {
        StockDecision.WrongRole => StockError.ApprovalForbidden,
        StockDecision.MissingEvidence => StockError.MissingEvidence,
        _ => StockError.NegativeStock
    };
}
