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
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            logger.LogWarning("Could not save a supplier deactivation for {SupplierId}.", supplier.Id);
            return Result<Guid, SupplierError>.Fail(SupplierError.NotFound);
        }

        logger.LogInformation("Deactivated supplier {SupplierId}.", supplier.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.SupplierDeactivated, "supplier", supplier.Id.ToString("D"), supplier.Code, cancellationToken);
        return Result<Guid, SupplierError>.Ok(supplier.Id);
    }
}

/// <summary>Khôi phục nhà cung cấp đã ngừng hợp tác. Chỉ Admin.</summary>
public sealed class ReactivateSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<ReactivateSupplierCommandHandler> logger,
    TimeProvider? clock = null,
    IRequestInfo? request = null,
    IAuditLog? audit = null)
{
    public async Task<Result<Guid, SupplierError>> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanReactivate(actor.Role))
        {
            logger.LogWarning("Supplier reactivation forbidden for role {Role}.", actor.Role);
            return Result<Guid, SupplierError>.Fail(SupplierError.Forbidden);
        }

        var supplier = await repository.GetAnyForUpdateAsync(id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<Guid, SupplierError>.Fail(SupplierError.NotFound);
        }

        if (supplier.IsActive)
        {
            return Result<Guid, SupplierError>.Ok(supplier.Id);
        }

        // Trong lúc ngừng hợp tác, mã có thể đã được nhà cung cấp khác dùng.
        if (await repository.NormalizedCodeExistsAsync(supplier.NormalizedCode, supplier.Id, cancellationToken))
        {
            logger.LogWarning("Cannot reactivate supplier {SupplierId}: code is used.", supplier.Id);
            return CodeUsed();
        }

        supplier.Reactivate();
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            return CodeUsed();
        }

        logger.LogInformation("Reactivated supplier {SupplierId}.", supplier.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.SupplierReactivated, "supplier", supplier.Id.ToString("D"), supplier.Code, cancellationToken);
        return Result<Guid, SupplierError>.Ok(supplier.Id);
    }

    private static Result<Guid, SupplierError> CodeUsed() =>
        Result<Guid, SupplierError>.Fail(SupplierError.DuplicateCode, [new SupplierIssue(SupplierField.Code, SupplierIssueKind.Duplicate)]);
}
