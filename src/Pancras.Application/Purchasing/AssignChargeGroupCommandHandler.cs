using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class AssignChargeGroupCommandHandler(
    IPurchaseOrderRepository orders,
    ILogger<AssignChargeGroupCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(AssignChargeGroupCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Charge group rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if (order.Status != PurchaseStatus.Open)
        {
            logger.LogWarning("Charge group rejected because purchase order {PurchaseOrderId} is already received.", order.Id);
            return Result<Guid>.Fail(PurchaseError.AlreadyReceived);
        }

        if (!order.TryAssignGroup(command.ChargeId, command.Group, command.RetainCurrent))
        {
            logger.LogWarning("Charge group kept for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.SplitGroup);
        }

        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Charge group commit failed for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        logger.LogInformation("Assigned charge group on purchase order {PurchaseOrderId}.", order.Id);
        return Result<Guid>.Ok(command.ChargeId);
    }
}
