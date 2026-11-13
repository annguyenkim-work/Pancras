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

        if (InventoryAccess.IsGlobal(command.Actor.Role))
        {
            // Admin, Giám đốc, Kế toán ghi thẳng: lập và duyệt trong một lần lưu. Nếu không duyệt được
            // (hết tồn, v.v.) thì không có phiếu nào được ghi, để phiếu chờ không thể duyệt lại sau này.
            var direct = await approveHandler.ApplyAsync(slip, command.Actor, isNew: true, cancellationToken);
            if (direct.IsFailure)
            {
                logger.LogWarning("Direct disposal for {CatalogItemId} was rejected and nothing was saved.", command.CatalogItemId);
                return Result<Guid>.Fail(direct.StockError);
            }

            logger.LogInformation("Created and approved disposal slip {DisposalSlipId} for {CatalogItemId}.", slip.Id, command.CatalogItemId);
            return Result<Guid>.Ok(slip.Id);
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
