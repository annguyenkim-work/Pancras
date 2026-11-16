using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

public sealed class CreateLocationCommandHandler(
    ILocationRepository repository,
    ILogger<CreateLocationCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<LocationOutcome> HandleAsync(
        CreateLocationCommand command,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (!LocationAccess.CanManage(actor.Role))
        {
            logger.LogWarning("Location creation forbidden for employee {EmployeeId}.", actor.Id);
            await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.AccessDenied, "stock_location", null, "Thêm kho/showroom", cancellationToken);
            return new LocationOutcome(null, LocationError.Forbidden);
        }

        var name = command.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return new LocationOutcome(null, LocationError.NameMissing);
        }

        if (name.Length > StockLocation.NameMaxLength)
        {
            return new LocationOutcome(null, LocationError.NameTooLong);
        }

        if (command.Kind is not StockLocationKind kind || !Enum.IsDefined(kind))
        {
            return new LocationOutcome(null, LocationError.InvalidKind);
        }

        if (await repository.NameExistsAsync(name, cancellationToken))
        {
            return new LocationOutcome(null, LocationError.DuplicateName);
        }

        var location = StockLocation.Create(name, kind);
        if (!await repository.TryAddAsync(location, cancellationToken))
        {
            return new LocationOutcome(null, LocationError.DuplicateName);
        }

        logger.LogInformation("Created stock location {LocationId}.", location.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.LocationCreated, "stock_location", location.Id.ToString("D"), null, cancellationToken);
        return new LocationOutcome(location.Id, LocationError.None);
    }
}
