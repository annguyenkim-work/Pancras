using Pancras.Application.Auditing;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class SetEmployeeActiveCommandHandler(
    IEmployeeRepository repository,
    ILogger<SetEmployeeActiveCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<EmployeeProfile, EmployeeError>> HandleAsync(
        Guid id,
        bool active,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(id, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (!EmployeeAccess.CanSetActive(actor, employee.Id, employee.Role))
        {
            logger.LogWarning("Employee status change forbidden for {ActorId} on {EmployeeId}.", actor.Id, employee.Id);
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.Forbidden);
        }

        var result = await repository.WithinPersonnelRulesAsync(
            ct => ApplyActiveChangeAsync(employee, active, ct),
            cancellationToken);
        if (result.IsSuccess)
        {
            var action = active
                ? Pancras.Domain.Auditing.AuditActions.EmployeeActivated
                : Pancras.Domain.Auditing.AuditActions.EmployeeDeactivated;
            await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, action, "employee", employee.Id.ToString("D"), active ? "activated" : null, cancellationToken);
        }

        return result;
    }

    private async Task<Result<EmployeeProfile, EmployeeError>> ApplyActiveChangeAsync(
        Employee employee,
        bool active,
        CancellationToken cancellationToken)
    {
        if (!active && employee.IsActive && employee.Role == EmployeeRole.Admin
            && await repository.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.LastAdmin);
        }

        if (active && !employee.IsActive
            && employee.Role == EmployeeRole.ShowroomWarehouseLead
            && employee.StockLocationId is Guid locationId
            && await repository.ActiveLeadExistsAsync(locationId, employee.Id, cancellationToken))
        {
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.LeadExists);
        }

        if (active)
        {
            employee.Activate();
        }
        else
        {
            employee.Deactivate();
        }

        if (await repository.TrySaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.VersionConflict);
        }

        logger.LogInformation("Set employee {EmployeeId} active={Active}.", employee.Id, active);
        return Result<EmployeeProfile, EmployeeError>.Ok(EmployeeMapping.ToProfile(employee));
    }
}
