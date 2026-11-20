using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateDisposalSlipCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ApproveDisposalSlipCommandHandler approveHandler,
    ILogger<CreateDisposalSlipCommandHandler> logger)
{
    public async Task<Result<Guid, StockError>> HandleAsync(CreateDisposalSlipCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        if (!InventoryAccess.CanOperate(command.Actor.Role)
            || !InventoryAccess.CanAccessLocation(command.Actor, command.StockLocationId))
        {
            logger.LogWarning("Disposal slip forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        var decision = DisposalSlip.TryCreate(
            command.StockLocationId,
            command.CatalogItemId,
            command.Quantity,
            command.Reason,
            command.DocumentRef,
            command.ImageRef,
            command.Actor.Role,
            out var slip);
        if (decision != StockDecision.Accepted || slip is null)
        {
            logger.LogInformation("Disposal slip rejected for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid, StockError>.Fail(StockDecisionErrors.ToError(decision));
        }

        if (InventoryAccess.IsGlobal(command.Actor.Role))
        {
            // Admin, Giám đốc, Kế toán ghi thẳng: lập và duyệt trong một lần lưu. Nếu không duyệt được
            // (hết tồn, v.v.) thì không có phiếu nào được ghi, để phiếu chờ không thể duyệt lại sau này.
            var direct = await approveHandler.ApplyAsync(slip, command.Actor, isNew: true, scope, cancellationToken);
            if (direct.IsFailure)
            {
                logger.LogInformation("Direct disposal for {CatalogItemId} was rejected and nothing was saved.", command.CatalogItemId);
                return Result<Guid, StockError>.Fail(direct.Error);
            }

            logger.LogInformation("Created and approved disposal slip {DisposalSlipId} for {CatalogItemId}.", slip.Id, command.CatalogItemId);
            return Result<Guid, StockError>.Ok(slip.Id);
        }

        await repository.AddDisposalAsync(slip, cancellationToken);
        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Disposal slip commit failed for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid, StockError>.Fail(StockError.MissingEvidence);
        }

        logger.LogInformation("Created disposal slip {DisposalSlipId} for {CatalogItemId}.", slip.Id, command.CatalogItemId);
        return Result<Guid, StockError>.Ok(slip.Id);
    }
}
