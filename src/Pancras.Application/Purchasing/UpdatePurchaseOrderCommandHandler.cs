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
        // Dòng trống hoàn toàn (chưa chọn mặt hàng, số lượng 0) bị bỏ qua. Dòng đã chọn mặt hàng mà số lượng <= 0 là lỗi:
        // không được âm thầm coi là "xóa dòng", vì dòng bị xóa kéo theo phần chia chi phí của nó.
        var submitted = command.Lines ?? [];
        if (submitted.Any(line => line.Quantity <= 0 && line.CatalogItemId != Guid.Empty))
        {
            return Result<Guid>.Fail(PurchaseError.InvalidQuantity);
        }

        var lines = submitted.Where(line => line.Quantity > 0).ToArray();
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

        // Xác định trước khoản nào bị xóa: khoản đã có mà không còn trong danh sách gửi lên, hoặc (khoản không phải chia tay)
        // gửi lên với số tiền 0. null nghĩa là không đụng tới chi phí.
        var submittedCharges = command.Charges;
        var chargeIdsToDrop = new HashSet<Guid>();
        if (submittedCharges is not null)
        {
            if (submittedCharges.Any(charge => charge.Amount < 0))
            {
                return Result<Guid>.Fail(PurchaseError.NegativeMoney);
            }

            if (submittedCharges.Any(charge => !Enum.IsDefined(charge.Group) || !Enum.IsDefined(charge.Method)
                || (charge.ChargeId is null && charge.Method == ChargeSplitMethod.Manual)))
            {
                return Result<Guid>.Fail(PurchaseError.InvalidOrder);
            }

            var sentIds = submittedCharges.Where(charge => charge.ChargeId is not null).Select(charge => charge.ChargeId!.Value).ToHashSet();
            foreach (var existing in order.Charges)
            {
                var sent = submittedCharges.FirstOrDefault(charge => charge.ChargeId == existing.Id);
                if (!sentIds.Contains(existing.Id) || (existing.Method != ChargeSplitMethod.Manual && sent!.Amount <= 0))
                {
                    chargeIdsToDrop.Add(existing.Id);
                }
            }
        }

        // Khoản chia tay còn lại không thể tự chia lại khi tập dòng đổi: chặn trước khi sửa bất cứ thứ gì.
        var keptIds = lines.Where(line => line.LineId is not null).Select(line => line.LineId!.Value).ToHashSet();
        var setChanges = lines.Any(line => line.LineId is null || order.Lines.All(item => item.Id != line.LineId))
            || order.Lines.Any(item => !keptIds.Contains(item.Id));
        if (setChanges && order.Charges.Any(charge => charge.Method == ChargeSplitMethod.Manual && !chargeIdsToDrop.Contains(charge.Id)))
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

        if (submittedCharges is not null)
        {
            foreach (var chargeId in chargeIdsToDrop)
            {
                order.DropCharge(chargeId);
            }

            foreach (var charge in submittedCharges.Where(charge => charge.Amount > 0 || charge.Method == ChargeSplitMethod.Manual))
            {
                if (charge.ChargeId is Guid existingId)
                {
                    var existing = order.Charges.FirstOrDefault(item => item.Id == existingId);
                    if (existing is null
                        || (existing.Method != ChargeSplitMethod.Manual && !order.TryUpdateCharge(existingId, charge.Amount, charge.Method, charge.Label))
                        || !order.TryAssignGroup(existingId, charge.Group, retainCurrent: false))
                    {
                        return Result<Guid>.Fail(PurchaseError.InvalidOrder);
                    }
                }
                else if (!await ChargeBasis.TryAddSplitAsync(order, charge.Amount, charge.Label, charge.Group, charge.Method, measures, cancellationToken))
                {
                    return Result<Guid>.Fail(PurchaseError.MissingMeasure);
                }
            }
        }

        // Chia lại các khoản theo số lượng, giá, số đo, số tiền, cách chia mới (khoản chia tay giữ nguyên vì tập dòng không đổi).
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

        order.StampUpdated((clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Purchase order update commit failed for {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.SaveFailed);
        }

        logger.LogInformation("Updated purchase order {PurchaseOrderId}.", order.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseUpdated, "purchase_order", order.Id.ToString("D"), null, cancellationToken);
        return Result<Guid>.Ok(order.Id);
    }

    private static bool HasNegativeMoney(CreatePurchaseLineInput line) =>
        line.FobUsd < 0 || line.DutyRate < 0 || line.SupplementalDuty < 0 || line.SpecialConsumptionTax < 0 || line.EnvironmentTax < 0;
}
