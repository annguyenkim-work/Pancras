using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed record EmployeeCard(
    Guid Id,
    string FullName,
    string Code,
    string Role,
    string Email,
    string Phone,
    Guid? StockLocationId,
    string LocationName,
    string Address,
    string JobTitle,
    string Department,
    bool IsActive,
    bool TwoFactorEnabled = false,
    DateTimeOffset? LockedUntil = null,
    AuditStampView? Stamp = null);

public sealed class ListEmployeesHandler(
    IEmployeeRepository repository,
    ILocationRepository locations,
    ILogger<ListEmployeesHandler> logger)
{
    public async Task<IReadOnlyList<EmployeeCard>> HandleAsync(
        EmployeeActor actor,
        string? search,
        string? role,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAccess.CanList(actor))
        {
            logger.LogWarning("Employee list forbidden for employee {EmployeeId}.", actor.Id);
            return [];
        }

        var scope = EmployeeAccess.ListScope(actor);
        if (EmployeeAccess.CanList(actor) && actor.Role == EmployeeRole.ShowroomWarehouseLead && scope is null)
        {
            return [];
        }

        var effectiveLocation = scope ?? locationId;
        var employees = await repository.ListReadOnlyAsync(cancellationToken);
        var names = (await locations.ListAsync(cancellationToken)).ToDictionary(location => location.Id, location => location.Name);
        var term = search?.Trim();
        var selectedRole = role?.Trim();
        var cards = employees
            .Where(employee =>
                (effectiveLocation is null || employee.StockLocationId == effectiveLocation)
                && (string.IsNullOrEmpty(term) || employee.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) || employee.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || employee.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrEmpty(selectedRole) || employee.Role == selectedRole))
            .OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(employee => new EmployeeCard(
                employee.Id,
                employee.FullName,
                employee.Code,
                employee.Role,
                employee.Email,
                employee.Phone,
                employee.StockLocationId,
                employee.StockLocationId is Guid id && names.TryGetValue(id, out var locationName) ? locationName : string.Empty,
                employee.Address,
                employee.JobTitle,
                employee.Department,
                employee.IsActive,
                employee.TwoFactorEnabled,
                employee.LockedUntil,
                AuditStampView.From(employee)))
            .ToArray();
        logger.LogInformation("Listed {EmployeeCount} employee cards.", cards.Length);
        return cards;
    }
}
