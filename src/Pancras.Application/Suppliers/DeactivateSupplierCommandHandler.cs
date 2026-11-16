using Pancras.Domain.Suppliers;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Suppliers;

/// <summary>Ngừng hợp tác với nhà cung cấp: ẩn khỏi danh sách chọn khi lập đơn, nhưng giữ lại để xem lịch sử.</summary>
public sealed class DeactivateSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<DeactivateSupplierCommandHandler> logger,
    ISupplierPurchaseStatsReader? purchaseStats = null,
    TimeProvider? clock = null,
    IRequestInfo? request = null,
    IAuditLog? audit = null)
{
    public async Task<Result<Guid, SupplierError>> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanManage(actor.Role))
        {
            logger.LogWarning("Supplier deactivation forbidden for role {Role}.", actor.Role);
            return Result<Guid, SupplierError>.Fail(SupplierError.Forbidden);
        }

        var supplier = await repository.GetActiveForUpdateAsync(id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<Guid, SupplierError>.Fail(SupplierError.NotFound);
        }

        // Còn đơn mua chưa nhập kho thì chưa được ngừng hợp tác, vì đơn đó vẫn cần nhà cung cấp.
        if (purchaseStats is not null)
        {
            var stats = await purchaseStats.ReadAsync([supplier.Id], cancellationToken);
            if (stats.TryGetValue(supplier.Id, out var row) && row.LotsNotReceived > 0)
            {
                logger.LogWarning("Supplier {SupplierId} still has {OpenOrders} open purchase orders.", supplier.Id, row.LotsNotReceived);
                return Result<Guid, SupplierError>.Fail(SupplierError.HasOpenPurchases);
            }
        }

        supplier.Deactivate();
        var saved = await repository.TrySaveAsync(cancellationToken);
        if (saved != SaveOutcome.Saved)
        {
            logger.LogWarning("Could not save a supplier deactivation for {SupplierId}.", supplier.Id);
            return Result<Guid, SupplierError>.Fail(saved == SaveOutcome.Conflict ? SupplierError.VersionConflict : SupplierError.NotFound);
        }

        logger.LogInformation("Deactivated supplier {SupplierId}.", supplier.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.SupplierDeactivated, "supplier", supplier.Id.ToString("D"), supplier.Code, cancellationToken);
        return Result<Guid, SupplierError>.Ok(supplier.Id);
    }
}
