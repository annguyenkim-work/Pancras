using Microsoft.Extensions.Logging;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class AllocateChargeCommandHandler(
    IPurchaseOrderRepository orders,
    GetCatalogMeasuresHandler measures,
    ILogger<AllocateChargeCommandHandler> logger)
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

        if (!order.TryAddCharge(command.Amount, command.Method, out var chargeId))
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

        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Charge commit failed for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        logger.LogInformation("Allocated charge {PurchaseChargeId} on purchase order {PurchaseOrderId}.", chargeId, order.Id);
        return Result<Guid>.Ok(chargeId);
    }

    private async Task<IReadOnlyDictionary<Guid, decimal>?> BasisAsync(
        Domain.Purchasing.PurchaseOrder order,
        AllocateChargeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Method == ChargeSplitMethod.Manual)
        {
            return command.ManualShares ?? new Dictionary<Guid, decimal>();
        }

        if (command.Method is ChargeSplitMethod.Weight or ChargeSplitMethod.Volume)
        {
            var ids = order.Lines.Select(line => line.CatalogItemId).Distinct().ToArray();
            var catalog = await measures.HandleAsync(ids, cancellationToken);
            var basis = new Dictionary<Guid, decimal>();
            foreach (var line in order.Lines)
            {
                if (!catalog.TryGetValue(line.CatalogItemId, out var measure))
                {
                    return null;
                }

                var unit = command.Method == ChargeSplitMethod.Weight ? measure.WeightKg : measure.VolumeM3;
                if (unit is null)
                {
                    return null;
                }

                basis[line.Id] = unit.Value * line.Quantity;
            }

            return basis;
        }

        return order.Lines.ToDictionary(
            line => line.Id,
            line => command.Method == ChargeSplitMethod.Quantity ? (decimal)line.Quantity : line.FobUsd);
    }
}
