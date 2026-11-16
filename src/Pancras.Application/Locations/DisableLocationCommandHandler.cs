using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

/// <summary>Disable (thay cho xóa): ẩn khỏi danh sách, giữ nguyên lịch sử. Không Disable khi còn tồn kho, nhân viên đang hoạt động, đơn mua hoặc phiếu chuyển chưa đóng liên quan đến kho.</summary>
public sealed class DisableLocationCommandHandler(
    ILocationManagementRepository repository,
    ILogger<DisableLocationCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<LocationManageError> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!LocationAccess.CanManage(actor.Role))
        {
            await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.AccessDenied, "stock_location", id.ToString("D"), "Xóa kho/showroom", cancellationToken);
            return LocationManageError.Forbidden;
        }

        var location = await repository.GetForUpdateAsync(id, cancellationToken);
        if (location is null)
        {
            return LocationManageError.NotFound;
        }

        if (await repository.HasStockAsync(id, cancellationToken))
        {
            return LocationManageError.HasStock;
        }

        if (await repository.HasActiveEmployeesAsync(id, cancellationToken))
        {
            return LocationManageError.HasActiveEmployees;
        }

        if (await repository.HasOpenDocumentsAsync(id, cancellationToken))
        {
            return LocationManageError.HasOpenDocuments;
        }

        location.Deactivate();
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            return LocationManageError.SaveFailed;
        }

        logger.LogInformation("Soft-deleted stock location {LocationId}.", id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.LocationDeleted, "stock_location", id.ToString("D"), null, cancellationToken);
        return LocationManageError.None;
    }
}
