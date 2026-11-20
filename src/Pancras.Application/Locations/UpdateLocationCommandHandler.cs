using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

public sealed class UpdateLocationCommandHandler(
    ILocationManagementRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateLocationCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<LocationManageError> HandleAsync(UpdateLocationCommand command, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!LocationAccess.CanManage(actor.Role))
        {
            await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.AccessDenied, "stock_location", command.Id.ToString("D"), "Sửa kho/showroom", cancellationToken);
            return LocationManageError.Forbidden;
        }

        var name = command.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return LocationManageError.NameMissing;
        }

        if (name.Length > StockLocation.NameMaxLength)
        {
            return LocationManageError.NameTooLong;
        }

        if (command.Kind is not StockLocationKind kind || !Enum.IsDefined(kind))
        {
            return LocationManageError.InvalidKind;
        }

        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var location = await repository.GetForUpdateAsync(command.Id, cancellationToken);
        if (location is null)
        {
            return LocationManageError.NotFound;
        }

        if (command.Version != location.Version)
        {
            logger.LogInformation("Rejected a stale location edit for {LocationId}.", location.Id);
            return LocationManageError.VersionConflict;
        }

        if (await repository.NameExistsAsync(name, command.Id, cancellationToken))
        {
            return LocationManageError.DuplicateName;
        }

        location.Update(name, kind);
        audit.Stage(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.LocationUpdated, "stock_location", location.Id.ToString("D"), null);
        var saved = await scope.CommitAsync(cancellationToken);
        if (saved != SaveOutcome.Saved)
        {
            return saved == SaveOutcome.Conflict ? LocationManageError.VersionConflict : LocationManageError.SaveFailed;
        }

        logger.LogInformation("Updated stock location {LocationId}.", location.Id);
        return LocationManageError.None;
    }
}
