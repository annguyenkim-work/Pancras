using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Application.Suppliers;
using Pancras.Domain.Auditing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>Sửa thông tin chung và dòng hàng của đơn chưa nhập kho. Các khoản chi phí được chia lại theo dòng mới.</summary>
public sealed class UpdatePurchaseOrderCommandHandler(
    IPurchaseOrderRepository orders,
    ISupplierRepository suppliers,
    IInventoryRepository inventory,
    ICatalogItemRepository catalog,
    GetCatalogMeasuresHandler measures,
    ILogger<UpdatePurchaseOrderCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(UpdatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var lines = (command.Lines ?? []).Where(line => line.Quantity > 0).ToArray();
        if (command.ExchangeRate < 0 || command.VatRate < 0 || lines.Any(HasNegativeMoney))
        {
            return Result<Guid>.Fail(PurchaseError.NegativeMoney);
        }

        if (lines.Any(line => line.FobUsd <= 0))
        {
            return Result<Guid>.Fail(PurchaseError.ZeroPrice);
        }

        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Purchase order update rejected for {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if (order.Status != PurchaseStatus.Open)
        {
            logger.LogWarning("Purchase order {PurchaseOrderId} is already received and cannot be edited.", order.Id);
            return Result<Guid>.Fail(PurchaseError.AlreadyReceived);
        }

        var supplier = await suppliers.GetActiveReadOnlyAsync(command.SupplierId, cancellationToken);
        var locations = await inventory.ListLocationsAsync(cancellationToken);
        if (supplier is null || locations.All(location => location.Id != command.ReceiveLocationId) || lines.Length == 0)
        {
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        foreach (var line in lines)
        {
            if (await catalog.GetByIdReadOnlyAsync(line.CatalogItemId, cancellationToken) is null)
            {
                return Result<Guid>.Fail(PurchaseError.InvalidOrder);
            }
        }

        var normalized = (command.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized != order.NormalizedCode && await orders.NormalizedCodeExistsAsync(normalized, cancellationToken))
        {
            return Result<Guid>.Fail(PurchaseError.DuplicateCode);
        }

        // Khoản chia tay không thể tự chia lại khi tập dòng đổi: chặn trước khi sửa bất cứ thứ gì.
        var keptIds = lines.Where(line => line.LineId is not null).Select(line => line.LineId!.Value).ToHashSet();
        var setChanges = lines.Any(line => line.LineId is null || order.Lines.All(item => item.Id != line.LineId))
            || order.Lines.Any(item => !keptIds.Contains(item.Id));
        if (setChanges && order.Charges.Any(charge => charge.Method == ChargeSplitMethod.Manual))
        {
            return Result<Guid>.Fail(PurchaseError.ManualChargeLocked);
        }

        if (!order.TryUpdateHeader(
                command.Code,
                command.SupplierId,
                command.ReceiveLocationId,
                command.ContainerNumber,
                command.BillOfLading,
                command.Port,
                command.ExpectedOn,
                command.ExchangeRate,
                command.VatRate,
                command.VatDeductible)
            || !order.TryUpdateLines(
                lines.Select(line => (line.LineId, line.CatalogItemId, line.Quantity, line.FobUsd, line.DutyRate, line.SupplementalDuty, line.SpecialConsumptionTax, line.EnvironmentTax)).ToArray(),
                out _))
        {
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        // Chia lại các khoản theo số lượng, giá, số đo mới (khoản chia tay giữ nguyên vì tập dòng không đổi).
        foreach (var charge in order.Charges.Where(charge => charge.Method != ChargeSplitMethod.Manual))
        {
            var basis = await ChargeBasis.ComputeAsync(order, charge.Method, null, measures, cancellationToken);
            if (basis is null)
            {
                return Result<Guid>.Fail(PurchaseError.MissingMeasure);
            }

            if (!order.TryAllocate(charge.Id, basis, false))
            {
                return Result<Guid>.Fail(PurchaseError.MissingMeasure);
            }
        }

        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Purchase order update commit failed for {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        logger.LogInformation("Updated purchase order {PurchaseOrderId}.", order.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseUpdated, "purchase_order", order.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(order.Id);
    }

    private static bool HasNegativeMoney(CreatePurchaseLineInput line) =>
        line.FobUsd < 0 || line.DutyRate < 0 || line.SupplementalDuty < 0 || line.SpecialConsumptionTax < 0 || line.EnvironmentTax < 0;
}

public sealed class DeletePurchaseChargeCommandHandler(
    IPurchaseOrderRepository orders,
    ILogger<DeletePurchaseChargeCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(DeletePurchaseChargeCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null || order.Charges.All(charge => charge.Id != command.ChargeId))
        {
            logger.LogWarning("Charge delete rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if (order.Status != PurchaseStatus.Open)
        {
            logger.LogWarning("Charge delete rejected because purchase order {PurchaseOrderId} is already received.", order.Id);
            return Result<Guid>.Fail(PurchaseError.AlreadyReceived);
        }

        order.DropCharge(command.ChargeId);
        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Charge delete commit failed for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        logger.LogInformation("Deleted charge {PurchaseChargeId} from purchase order {PurchaseOrderId}.", command.ChargeId, order.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseChargeDeleted, "purchase_order", order.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(command.ChargeId);
    }
}
