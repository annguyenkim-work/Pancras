using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

public interface ILocationRepository
{
    Task<IReadOnlyList<StockLocation>> ListAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(StockLocation location, CancellationToken cancellationToken);
}

public enum LocationError
{
    None = 0,
    Forbidden,
    NameMissing,
    NameTooLong,
    InvalidKind,
    DuplicateName
}

public sealed record LocationOutcome(Guid? Id, LocationError Error)
{
    public bool IsSuccess => Error == LocationError.None;
}

public sealed class CreateLocationCommand
{
    public string? Name { get; set; }

    public StockLocationKind Kind { get; set; } = StockLocationKind.Warehouse;
}

public sealed class ListLocationsHandler(ILocationRepository repository)
{
    public Task<IReadOnlyList<StockLocation>> HandleAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);
}

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

        if (!Enum.IsDefined(command.Kind))
        {
            return new LocationOutcome(null, LocationError.InvalidKind);
        }

        if (await repository.NameExistsAsync(name, cancellationToken))
        {
            return new LocationOutcome(null, LocationError.DuplicateName);
        }

        var location = StockLocation.Create(name, command.Kind);
        if (!await repository.TryAddAsync(location, cancellationToken))
        {
            return new LocationOutcome(null, LocationError.DuplicateName);
        }

        logger.LogInformation("Created stock location {LocationId}.", location.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.LocationCreated, "stock_location", location.Id.ToString("D"), null, cancellationToken);
        return new LocationOutcome(location.Id, LocationError.None);
    }
}
