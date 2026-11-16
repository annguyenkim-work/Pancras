using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class ResetTwoFactorCommandHandler(
    IEmployeeRepository repository,
    ILogger<ResetTwoFactorCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<EmployeeProfile, EmployeeError>> HandleAsync(Guid employeeId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!EmployeeAccess.CanResetPassword(actor))
        {
            logger.LogWarning("Two-factor reset forbidden for employee {EmployeeId}.", actor.Id);
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.Forbidden);
        }

        var employee = await repository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        employee.DisableTwoFactor();
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Two-factor of employee {EmployeeId} was reset by {ActorId}.", employee.Id, actor.Id);
        await audit.RecordAsync(_clock, request, actor.Id, null, AuditActions.TwoFactorReset, "employee", employee.Id.ToString("D"), null, cancellationToken);
        return Result<EmployeeProfile, EmployeeError>.Ok(EmployeeMapping.ToProfile(employee));
    }
}
