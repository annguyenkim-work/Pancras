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

/// <summary>Sửa và xóa mềm kho/showroom (tách khỏi <see cref="ILocationRepository"/> để không ảnh hưởng nơi chỉ cần đọc).</summary>
public interface ILocationManagementRepository
{
    Task<StockLocation?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid excludingId, CancellationToken cancellationToken);

    Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasActiveEmployeesAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
}

public enum LocationManageError
{
    None = 0,
    Forbidden,
    NotFound,
    NameMissing,
    NameTooLong,
    InvalidKind,
    DuplicateName,
    HasStock,
    HasActiveEmployees,
    SaveFailed
}

public sealed class UpdateLocationCommand
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public StockLocationKind Kind { get; set; } = StockLocationKind.Warehouse;
}

public sealed class UpdateLocationCommandHandler(
    ILocationManagementRepository repository,
    ILogger<UpdateLocationCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<LocationManageError> HandleAsync(UpdateLocationCommand command, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!LocationAccess.CanManage(actor.Role))
        {
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

        if (!Enum.IsDefined(command.Kind))
        {
            return LocationManageError.InvalidKind;
        }

        var location = await repository.GetForUpdateAsync(command.Id, cancellationToken);
        if (location is null)
        {
            return LocationManageError.NotFound;
        }

        if (await repository.NameExistsAsync(name, command.Id, cancellationToken))
        {
            return LocationManageError.DuplicateName;
        }

        location.Update(name, command.Kind);
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            return LocationManageError.SaveFailed;
        }

        logger.LogInformation("Updated stock location {LocationId}.", location.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.LocationUpdated, "stock_location", location.Id.ToString("D"), null, cancellationToken);
        return LocationManageError.None;
    }
}

/// <summary>Xóa mềm: ẩn khỏi danh sách, giữ nguyên lịch sử. Không xóa khi còn tồn kho hoặc còn nhân viên đang hoạt động gắn với kho.</summary>
public sealed class DeleteLocationCommandHandler(
    ILocationManagementRepository repository,
    ILogger<DeleteLocationCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<LocationManageError> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!LocationAccess.CanManage(actor.Role))
        {
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

        location.MarkDeleted();
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            return LocationManageError.SaveFailed;
        }

        logger.LogInformation("Soft-deleted stock location {LocationId}.", id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, Pancras.Domain.Auditing.AuditActions.LocationDeleted, "stock_location", id.ToString("D"), null, cancellationToken);
        return LocationManageError.None;
    }
}
