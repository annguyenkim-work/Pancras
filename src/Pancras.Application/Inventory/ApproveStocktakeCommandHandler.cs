using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ApproveStocktakeCommandHandler(
    IInventoryRepository repository,
    ILogger<ApproveStocktakeCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(ApproveDocumentCommand command, CancellationToken cancellationToken)
    {
        var sheet = await repository.FindStocktakeForUpdateAsync(command.Id, cancellationToken);
        if (sheet is null)
        {
            logger.LogWarning("Stocktake approval rejected for {StocktakeId}.", command.Id);
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        var status = sheet.Status;
        var decision = sheet.Approve(command.Role);
        if (decision != StockDecision.Accepted)
        {
            logger.LogWarning("Stocktake approval rejected for {StocktakeId}.", sheet.Id);
            return Result<Guid>.Fail(decision == StockDecision.WrongRole ? StockError.ApprovalForbidden : StockError.ApprovalForbidden);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            sheet.RestoreUncommitted(status);
            logger.LogWarning("Stocktake approval commit failed for {StocktakeId}.", sheet.Id);
            return Result<Guid>.Fail(StockError.ApprovalForbidden);
        }

        logger.LogInformation("Approved stocktake {StocktakeId}.", sheet.Id);
        return Result<Guid>.Ok(sheet.Id);
    }
}
