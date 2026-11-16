using Pancras.Application.Auditing;
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
    ILogger<CreatePurchaseOrderCommandHandler> logger,
    GetCatalogMeasuresHandler? measures = null,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IRequestInfo? request = null,
    Pancras.Application.Auditing.IAuditLog? audit = null)
{
    public async Task<Result<Guid, PurchaseError>> HandleAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        if (!Pancras.Domain.Purchasing.PurchaseAccess.CanManage(command.Actor))
        {
            logger.LogWarning("Purchase order creation forbidden for role {Role}.", command.Actor.Role);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        var supplier = await suppliers.GetActiveReadOnlyAsync(command.SupplierId, cancellationToken);
        var locations = await inventory.ListLocationsAsync(cancellationToken);
        if (supplier is null || locations.All(location => location.Id != command.ReceiveLocationId))
        {
            logger.LogWarning("Purchase order rejected for supplier or location.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
        }

        var submitted = command.Lines ?? [];
        if (submitted.Any(line => line.Quantity <= 0 && line.CatalogItemId != Guid.Empty))
        {
            logger.LogWarning("Purchase order rejected because a selected line has no quantity.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidQuantity);
        }

        // Kiểm tra số lượng trước số tiền: số lượng âm làm thành tiền (đơn giá x số lượng) âm theo và không được báo là lỗi tiền.
        if (HasNegativeMoney(command))
        {
            logger.LogWarning("Purchase order rejected for negative money.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.NegativeMoney);
        }

        var usable = submitted.Where(line => line.Quantity > 0).ToArray();
        if (usable.Length == 0)
        {
            logger.LogWarning("Purchase order rejected because it has no line.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
        }

        if (usable.Any(line => line.FobUsd <= 0))
        {
            logger.LogWarning("Purchase order rejected because a line has no FOB price.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.ZeroPrice);
        }

        if (!PurchaseMoney.FitsLimits(
                command.ExchangeRate,
                command.VatRate,
                usable.Select(line => (line.FobUsd, line.DutyRate, line.SupplementalDuty, line.SpecialConsumptionTax, line.EnvironmentTax)),
                (command.Charges ?? []).Select(charge => charge.Amount)))
        {
            logger.LogWarning("Purchase order rejected because a number is too large.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AmountTooLarge);
        }

        foreach (var line in usable)
        {
            var item = await catalog.GetByIdReadOnlyAsync(line.CatalogItemId, cancellationToken);
            if (item is null)
            {
                logger.LogWarning("Purchase order rejected for catalog item {CatalogItemId}.", line.CatalogItemId);
                return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
            }
        }

        var normalized = (command.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (await orders.NormalizedCodeExistsAsync(normalized, cancellationToken))
        {
            logger.LogWarning("Purchase order code is already used.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.DuplicateCode);
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
                line.EnvironmentTax)).ToArray(),
            command.VatDeductible,
            (clock ?? TimeProvider.System).GetUtcNow(),
            request?.ActorCode,
            command.ExchangeRateOn,
            command.ExchangeRateSource);
        if (order is null)
        {
            logger.LogWarning("Purchase order was rejected.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
        }

        // Chứng từ giữ đúng mã và tên nhà cung cấp tại thời điểm lập đơn, dù hồ sơ nhà cung cấp đổi sau này.
        order.RecordSupplier(supplier.Code, supplier.Name);

        // Chi phí ban đầu được thêm trước khi lưu: đơn và chi phí vào DB trong cùng một lần lưu, hoặc không có gì cả.
        foreach (var charge in command.Charges ?? [])
        {
            if (charge.Amount > 0 && !await TryAddChargeAsync(order, charge, cancellationToken))
            {
                logger.LogWarning("Purchase order rejected because an initial charge could not be split.");
                var error = charge.Method is ChargeSplitMethod.Weight or ChargeSplitMethod.Volume
                    ? PurchaseError.MissingMeasure
                    : PurchaseError.ChargeFailed;
                return Result<Guid, PurchaseError>.Fail(error);
            }
        }

        if (!await orders.TryAddAsync(order, cancellationToken))
        {
            logger.LogWarning("Purchase order code is already used.");
            return Result<Guid, PurchaseError>.Fail(PurchaseError.DuplicateCode);
        }

        logger.LogInformation("Created purchase order {PurchaseOrderId}.", order.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, Pancras.Domain.Auditing.AuditActions.PurchaseCreated, "purchase_order", order.Id.ToString("D"), null, cancellationToken);
        return Result<Guid, PurchaseError>.Ok(order.Id);
    }

    private Task<bool> TryAddChargeAsync(PurchaseOrder order, CreatePurchaseChargeInput charge, CancellationToken cancellationToken) =>
        ChargeBasis.TryAddSplitAsync(order, charge.Amount, charge.Label, charge.Group, charge.Method, measures, cancellationToken);

    private static bool HasNegativeMoney(CreatePurchaseOrderCommand command) =>
        command.ExchangeRate < 0
        || command.VatRate < 0
        || (command.Charges ?? []).Any(charge => charge.Amount < 0)
        || (command.Lines ?? []).Any(line =>
            line.FobUsd < 0
            || line.DutyRate < 0
            || line.SupplementalDuty < 0
            || line.SpecialConsumptionTax < 0
            || line.EnvironmentTax < 0);
}
