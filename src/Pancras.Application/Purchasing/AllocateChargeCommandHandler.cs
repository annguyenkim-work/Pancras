using Microsoft.Extensions.Logging;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class AllocateChargeCommandHandler(
    IPurchaseOrderRepository orders,
    GetCatalogMeasuresHandler measures,
    ILogger<AllocateChargeCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(AllocateChargeCommand command, CancellationToken cancellationToken)
    {
        if (command.Amount < 0)
        {
            logger.LogWarning("Charge rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.NegativeMoney);
        }

        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null || !Enum.IsDefined(command.Method))
        {
            logger.LogWarning("Charge rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if (order.Status != PurchaseStatus.Open)
        {
            logger.LogWarning("Charge rejected because purchase order {PurchaseOrderId} is already received.", order.Id);
            return Result<Guid>.Fail(PurchaseError.AlreadyReceived);
        }

        if (command.Group is ChargeGroup requested && !Enum.IsDefined(requested))
        {
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if (!order.TryAddCharge(command.Amount, command.Method, out var chargeId, command.Label))
        {
            logger.LogWarning("Charge rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        var basis = await BasisAsync(order, command, cancellationToken);
        if (basis is null)
        {
            order.DropCharge(chargeId);
            logger.LogWarning("Charge measure missing for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.MissingMeasure);
        }

        var manual = command.Method == ChargeSplitMethod.Manual;
        if (!order.TryAllocate(chargeId, basis, manual))
        {
            order.DropCharge(chargeId);
            logger.LogWarning("Charge allocation rejected for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(manual ? PurchaseError.InvalidOrder : PurchaseError.MissingMeasure);
        }

        if (command.Group is ChargeGroup group)
        {
            order.TryAssignGroup(chargeId, group, false);
        }

        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Charge commit failed for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.SaveFailed);
        }

        logger.LogInformation("Allocated charge {PurchaseChargeId} on purchase order {PurchaseOrderId}.", chargeId, order.Id);
        await Pancras.Application.Auditing.AuditLogExtensions.RecordAsync(audit, clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.PurchaseChargeAdded, "purchase_order", order.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(chargeId);
    }

    private Task<IReadOnlyDictionary<Guid, decimal>?> BasisAsync(
        Domain.Purchasing.PurchaseOrder order,
        AllocateChargeCommand command,
        CancellationToken cancellationToken) =>
        ChargeBasis.ComputeAsync(order, command.Method, command.ManualShares, measures, cancellationToken);
}
