using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>Dữ liệu để xuất file một đơn mua. <see cref="IncludeCost"/> sai thì file không được chứa giá FOB và giá vốn.</summary>
public sealed record PurchaseExport(PurchaseOrderDetail Order, bool IncludeCost);

/// <summary>Lấy đơn mua để xuất file: kiểm tra quyền và ghi nhật ký (file chứa giá vốn).</summary>
public sealed class ExportPurchaseOrderHandler(
    GetPurchaseOrderHandler getOrder,
    ILogger<ExportPurchaseOrderHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<PurchaseExport, PurchaseError>> HandleAsync(Guid purchaseOrderId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(actor) && !PurchaseAccess.IsWarehouseStaff(actor))
        {
            logger.LogWarning("Purchase export forbidden for role {Role}.", actor.Role);
            return Result<PurchaseExport, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        var detail = await getOrder.HandleAsync(purchaseOrderId, actor, cancellationToken);
        if (detail is null)
        {
            return Result<PurchaseExport, PurchaseError>.Fail(PurchaseError.InvalidOrder);
        }

        var includeCost = detail.CostVisible;
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.PurchaseExported, "purchase_order", detail.Id.ToString("D"), includeCost ? "có giá vốn" : "không có giá vốn", cancellationToken);
        logger.LogInformation("Exported purchase order {PurchaseOrderId}.", detail.Id);
        return Result<PurchaseExport, PurchaseError>.Ok(new PurchaseExport(detail, includeCost));
    }
}
