using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;

namespace Pancras.Application.Catalog;

/// <summary>
/// Disable / Active một mặt hàng. Mặt hàng Disable bị ẩn khỏi danh sách chọn (đơn mua, phiếu kho), vẫn giữ để xem lịch sử.
/// Form gửi <c>Version</c> lúc mở trang; lệch nghĩa là có người sửa trước.
/// </summary>
public sealed class SetCatalogItemActiveCommandHandler(
    ICatalogItemRepository repository,
    ILogger<SetCatalogItemActiveCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<CatalogError> HandleAsync(
        Guid id,
        bool active,
        int version,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (!CatalogAccess.CanDisable(actor.Role))
        {
            logger.LogWarning("Rejected a catalog Disable/Active change for role {Role}.", actor.Role);
            await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.AccessDenied, "catalog_item", id.ToString("D"), "Disable/Active mặt hàng", cancellationToken);
            return CatalogError.Forbidden;
        }

        var item = await repository.GetVisibleForUpdateAsync(id, cancellationToken);
        if (item is null)
        {
            logger.LogInformation("Catalog item {CatalogItemId} was not found.", id);
            return CatalogError.NotFound;
        }

        if (version != item.Version)
        {
            logger.LogInformation("Rejected a stale catalog Disable/Active change for {CatalogItemId}.", item.Id);
            return CatalogError.VersionConflict;
        }

        if (item.IsActive == active)
        {
            return CatalogError.None;
        }

        if (active)
        {
            item.Reactivate();
        }
        else
        {
            item.Deactivate();
        }

        if (!await repository.TrySaveAsync(cancellationToken))
        {
            return CatalogError.VersionConflict;
        }

        logger.LogInformation("Catalog item {CatalogItemId} set to active={Active}.", item.Id, active);
        await audit.RecordAsync(
            clock ?? TimeProvider.System,
            request,
            actor.Id,
            null,
            active ? AuditActions.CatalogItemActivated : AuditActions.CatalogItemDisabled,
            "catalog_item",
            item.Id.ToString("D"),
            null,
            cancellationToken);
        return CatalogError.None;
    }
}
