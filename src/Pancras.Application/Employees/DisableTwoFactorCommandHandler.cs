using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class DisableTwoFactorCommandHandler(
    IEmployeeRepository repository,
    IPasswordHasher passwordHasher,
    ISecretProtector protector,
    ILogger<DisableTwoFactorCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<EmployeeSession, EmployeeError>> HandleAsync(
        DisableTwoFactorCommand command,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(actor.Id, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (!employee.TwoFactorEnabled)
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.TwoFactorNotEnabled);
        }

        if (string.IsNullOrEmpty(command.CurrentPassword) || !passwordHasher.Verify(command.CurrentPassword, employee.PasswordHash))
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.WrongPassword);
        }

        var now = _clock.GetUtcNow();
        var secret = Totp.FromBase32(protector.Unprotect(employee.TotpSecret));
        var valid = secret is not null
            && Totp.Verify(secret, command.Code, now, employee.LastTotpStep) is long;
        if (!valid && RecoveryCodes.Normalize(command.Code).Length >= 8)
        {
            valid = employee.TryUseRecoveryCode(command.Code!);
        }

        if (!valid)
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.InvalidCode);
        }

        employee.DisableTwoFactor();
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Employee {EmployeeId} disabled two-factor.", employee.Id);
        await audit.RecordAsync(_clock, request, employee.Id, employee.Code, AuditActions.TwoFactorDisabled, "employee", employee.Id.ToString("D"), null, cancellationToken);
        return Result<EmployeeSession, EmployeeError>.Ok(LoginEmployeeCommandHandler.ToSession(employee, requiresTwoFactor: false));
    }
}
