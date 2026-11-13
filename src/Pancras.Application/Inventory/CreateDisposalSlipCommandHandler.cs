using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateDisposalSlipCommandHandler(
    IInventoryRepository repository,
    ILogger<CreateDisposalSlipCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(CreateDisposalSlipCommand command, CancellationToken cancellationToken)
    {
        var decision = DisposalSlip.TryCreate(
            command.StockLocationId,
            command.CatalogItemId,
            command.Reason,
            command.DocumentRef,
            command.ImageRef,
            out var slip);
        if (decision != StockDecision.Accepted || slip is null)
        {
            logger.LogWarning("Disposal slip rejected for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid>.Fail(decision == StockDecision.MissingEvidence ? StockError.MissingEvidence : StockError.DisposalRejected);
        }

        await repository.AddDisposalAsync(slip, cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Disposal slip commit failed for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid>.Fail(StockError.MissingEvidence);
        }

        logger.LogInformation("Created disposal slip {DisposalSlipId} for {CatalogItemId}.", slip.Id, command.CatalogItemId);
        return Result<Guid>.Ok(slip.Id);
    }
}
