using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateDisposalSlipCommandHandler(
    IInventoryRepository repository,
    ApproveDisposalSlipCommandHandler approveHandler,
    ILogger<CreateDisposalSlipCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(CreateDisposalSlipCommand command, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(command.Actor.Role)
            || !InventoryAccess.CanAccessLocation(command.Actor, command.StockLocationId))
        {
            logger.LogWarning("Disposal slip forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var decision = DisposalSlip.TryCreate(
            command.StockLocationId,
            command.CatalogItemId,
            command.Reason,
            command.DocumentRef,
            command.ImageRef,
            command.Actor.Role,
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

        if (InventoryAccess.IsGlobal(command.Actor.Role))
        {
            // Admin, Giám đốc, Kế toán ghi thẳng: duyệt ngay phiếu vừa lập.
            var approved = await approveHandler.HandleAsync(new ApproveDocumentCommand(slip.Id, command.Actor), cancellationToken);
            if (approved.IsFailure)
            {
                logger.LogWarning("Direct disposal of {DisposalSlipId} was left pending.", slip.Id);
                return Result<Guid>.Fail(approved.StockError);
            }
        }

        return Result<Guid>.Ok(slip.Id);
    }
}
