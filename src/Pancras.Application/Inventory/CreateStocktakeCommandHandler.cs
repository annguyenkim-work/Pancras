using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class CreateStocktakeCommandHandler(
    IInventoryRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<CreateStocktakeCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Employees.IEmployeeRepository? employees = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid, StockError>> HandleAsync(CreateStocktakeCommand command, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        if (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, command.StockLocationId))
        {
            logger.LogWarning("Stocktake forbidden for employee {EmployeeId}.", command.Actor.Id);
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        if (command.CountedQuantity < 0)
        {
            return Result<Guid, StockError>.Fail(StockError.InvalidCount);
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
            logger.LogInformation("Stocktake rejected for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid, StockError>.Fail(StockDecisionErrors.ToError(decision));
        }

        // Người lập thuộc nhóm tự duyệt (Kế toán, Giám đốc, Admin): biên bản có hiệu lực ngay, áp luôn số đếm vào tồn.
        if (sheet.Status == ApprovalStatus.Approved)
        {
            var applied = await StocktakeApplication.ApplyAsync(
                repository, sheet, (clock ?? TimeProvider.System).GetUtcNow(), cancellationToken);
            if (!applied)
            {
                logger.LogInformation("Stocktake would drop stock below the reserved quantity for {CatalogItemId}.", command.CatalogItemId);
                return Result<Guid, StockError>.Fail(StockError.NegativeStock);
            }
        }

        await repository.AddStocktakeAsync(sheet, cancellationToken);
        audit.Stage(
            clock ?? TimeProvider.System,
            request,
            null,
            null,
            sheet.Status == ApprovalStatus.Approved
                ? Pancras.Domain.Auditing.AuditActions.StocktakeApproved
                : Pancras.Domain.Auditing.AuditActions.StocktakeRequested,
            "stocktake",
            sheet.Id.ToString("D"),
            null);
        if (sheet.Status == ApprovalStatus.Pending && employees is not null)
        {
            // Biên bản của nhân viên chờ duyệt: báo Trưởng Showroom/Kho đúng kho đó (trừ người lập).
            var at = (clock ?? TimeProvider.System).GetUtcNow();
            var people = (await employees.ListReadOnlyAsync(cancellationToken))
                .Where(person => person.IsActive && person.Id != command.Actor.Id
                    && InventoryRoles.IsShowroomLead(person.Role) && person.StockLocationId == sheet.StockLocationId);
            foreach (var person in people)
            {
                await repository.AddNoticeAsync(
                    Pancras.Domain.Notifications.Notification.Create(person.Id, sheet.StockLocationId, sheet.Id, Pancras.Domain.Notifications.NotificationKind.StocktakePending, Pancras.Domain.Notifications.Notification.StocktakePendingStep, at),
                    cancellationToken);
            }
        }

        if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
        {
            logger.LogWarning("Stocktake commit failed for {CatalogItemId}.", command.CatalogItemId);
            return Result<Guid, StockError>.Fail(StockError.MissingEvidence);
        }

        logger.LogInformation("Created stocktake {StocktakeId} for {CatalogItemId}.", sheet.Id, command.CatalogItemId);
        return Result<Guid, StockError>.Ok(sheet.Id);
    }
}
