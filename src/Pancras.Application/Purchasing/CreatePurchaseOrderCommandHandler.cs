using Microsoft.Extensions.Logging;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Application.Suppliers;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class CreatePurchaseOrderCommandHandler(
    IPurchaseOrderRepository orders,
    ISupplierRepository suppliers,
    IInventoryRepository inventory,
    ICatalogItemRepository catalog,
    ILogger<CreatePurchaseOrderCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        if (HasNegativeMoney(command))
        {
            logger.LogWarning("Purchase order rejected for negative money.");
            return Result<Guid>.Fail(PurchaseError.NegativeMoney);
        }

        var supplier = await suppliers.GetActiveReadOnlyAsync(command.SupplierId, cancellationToken);
        var locations = await inventory.ListLocationsAsync(cancellationToken);
        if (supplier is null || locations.All(location => location.Id != command.ReceiveLocationId))
        {
            logger.LogWarning("Purchase order rejected for supplier or location.");
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        var usable = (command.Lines ?? []).Where(line => line.Quantity > 0).ToArray();
        if (usable.Length == 0)
        {
            logger.LogWarning("Purchase order rejected because it has no line.");
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        foreach (var line in usable)
        {
            var item = await catalog.GetByIdReadOnlyAsync(line.CatalogItemId, cancellationToken);
            if (item is null)
            {
                logger.LogWarning("Purchase order rejected for catalog item {CatalogItemId}.", line.CatalogItemId);
                return Result<Guid>.Fail(PurchaseError.InvalidOrder);
            }
        }

        var normalized = (command.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (await orders.NormalizedCodeExistsAsync(normalized, cancellationToken))
        {
            logger.LogWarning("Purchase order code is already used.");
            return Result<Guid>.Fail(PurchaseError.DuplicateCode);
        }

        var order = PurchaseOrder.TryCreate(
            command.Code,
            command.SupplierId,
            command.ReceiveLocationId,
            command.ContainerNumber,
            command.BillOfLading,
            command.Port,
            command.ExpectedOn,
            command.ExchangeRate,
            command.VatRate,
            usable.Select(line => (
                line.CatalogItemId,
                line.Quantity,
                line.FobUsd,
                line.DutyRate,
                line.SupplementalDuty,
                line.SpecialConsumptionTax,
                line.EnvironmentTax)).ToArray());
        if (order is null)
        {
            logger.LogWarning("Purchase order was rejected.");
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if (!await orders.TryAddAsync(order, cancellationToken))
        {
            logger.LogWarning("Purchase order code is already used.");
            return Result<Guid>.Fail(PurchaseError.DuplicateCode);
        }

        logger.LogInformation("Created purchase order {PurchaseOrderId}.", order.Id);
        return Result<Guid>.Ok(order.Id);
    }

    private static bool HasNegativeMoney(CreatePurchaseOrderCommand command) =>
        command.ExchangeRate < 0
        || command.VatRate < 0
        || (command.Lines ?? []).Any(line =>
            line.FobUsd < 0
            || line.DutyRate < 0
            || line.SupplementalDuty < 0
            || line.SpecialConsumptionTax < 0
            || line.EnvironmentTax < 0);
}
