using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

/// <summary>Disable (thay cho xóa): ẩn khỏi danh sách, giữ nguyên lịch sử. Không Disable khi còn tồn kho, nhân viên đang hoạt động, đơn mua hoặc phiếu chuyển chưa đóng liên quan đến kho.</summary>
public sealed class DisableLocationCommandHandler(
    ILocationManagementRepository repository,
    IUnitOfWork unitOfWork,
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

        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
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
        audit.Stage(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.LocationDeleted, "stock_location", id.ToString("D"), null);
        var saved = await scope.CommitAsync(cancellationToken);
        if (saved != SaveOutcome.Saved)
        {
            return saved == SaveOutcome.Conflict ? LocationManageError.VersionConflict : LocationManageError.SaveFailed;
        }

        logger.LogInformation("Soft-deleted stock location {LocationId}.", id);
        return LocationManageError.None;
    }
}
