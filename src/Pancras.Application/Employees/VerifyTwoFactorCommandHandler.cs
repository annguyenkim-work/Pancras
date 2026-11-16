using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class VerifyTwoFactorCommandHandler(
    IEmployeeRepository repository,
    ISecretProtector protector,
    ILogger<VerifyTwoFactorCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<EmployeeSession, LoginError>> HandleAsync(VerifyTwoFactorCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var employee = await repository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null || !employee.IsActive || !employee.TwoFactorEnabled || employee.IsLockedOut(now))
        {
            return Result<EmployeeSession, LoginError>.Fail(LoginError.InvalidCredentials);
        }

        if (string.IsNullOrWhiteSpace(command.Code))
        {
            return Result<EmployeeSession, LoginError>.Fail(LoginError.InvalidCode);
        }

        var usedRecovery = false;
        var valid = false;
        var secret = Totp.FromBase32(protector.Unprotect(employee.TotpSecret));
        if (secret is not null)
        {
            var step = Totp.Verify(secret, command.Code, now, employee.LastTotpStep);
            valid = step is long matched && employee.TryUseTotpStep(matched);
        }

        if (!valid && RecoveryCodes.Normalize(command.Code).Length >= 8 && employee.TryUseRecoveryCode(command.Code))
        {
            valid = true;
            usedRecovery = true;
        }

        if (!valid)
        {
            var locked = employee.RegisterFailedLogin(now);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Two-factor code rejected for employee {EmployeeId}.", employee.Id);
            await audit.RecordAsync(_clock, request, employee.Id, employee.Code, locked ? AuditActions.LoginLockedOut : AuditActions.TwoFactorFailed, "employee", employee.Id.ToString("D"), null, cancellationToken);
            return Result<EmployeeSession, LoginError>.Fail(LoginError.InvalidCode);
        }

        employee.RegisterSuccessfulLogin();
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Login succeeded for employee {EmployeeId} with two-factor.", employee.Id);
        if (usedRecovery)
        {
            await audit.RecordAsync(_clock, request, employee.Id, employee.Code, AuditActions.RecoveryCodeUsed, "employee", employee.Id.ToString("D"), $"{employee.RecoveryCodesLeft} left", cancellationToken);
        }

        await audit.RecordAsync(_clock, request, employee.Id, employee.Code, AuditActions.LoginSucceeded, "employee", employee.Id.ToString("D"), "2fa", cancellationToken);
        return Result<EmployeeSession, LoginError>.Ok(LoginEmployeeCommandHandler.ToSession(employee, requiresTwoFactor: false));
    }
}
