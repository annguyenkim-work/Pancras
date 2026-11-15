using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class LoginEmployeeCommandValidator : AbstractValidator<LoginEmployeeCommand>
{
    public LoginEmployeeCommandValidator()
    {
        RuleFor(command => command.EmployeeCode)
            .Must(code => !string.IsNullOrWhiteSpace(code));
        RuleFor(command => command.Password)
            .Must(password => !string.IsNullOrEmpty(password));
    }
}

public sealed class LoginEmployeeCommandHandler(
    IEmployeeRepository repository,
    IPasswordHasher passwordHasher,
    ILogger<LoginEmployeeCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    private readonly LoginEmployeeCommandValidator _validator = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<EmployeeSession>> HandleAsync(
        LoginEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        if (!_validator.Validate(command).IsValid)
        {
            logger.LogInformation("Login rejected because a field was empty.");
            return Result<EmployeeSession>.Fail(LoginError.MissingCredentials);
        }

        var now = _clock.GetUtcNow();
        var normalizedCode = command.EmployeeCode!.Trim().ToUpperInvariant();
        var employee = await repository.GetByNormalizedCodeForUpdateAsync(normalizedCode, cancellationToken);
        if (employee is null || !employee.IsActive || employee.IsLockedOut(now))
        {
            // Băm một lần để thời gian trả lời không tiết lộ tài khoản có tồn tại hay không.
            _ = passwordHasher.Hash(command.Password!);
            logger.LogInformation("Login rejected.");
            await Log(employee, AuditActions.LoginFailed, employee is null ? "unknown account" : employee.IsActive ? "locked" : "inactive", cancellationToken);
            return Result<EmployeeSession>.Fail(LoginError.InvalidCredentials);
        }

        if (!passwordHasher.Verify(command.Password!, employee.PasswordHash))
        {
            var locked = employee.RegisterFailedLogin(now);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Login rejected because the credentials did not match.");
            await Log(employee, locked ? AuditActions.LoginLockedOut : AuditActions.LoginFailed, "wrong password", cancellationToken);
            return Result<EmployeeSession>.Fail(LoginError.InvalidCredentials);
        }

        if (employee.TwoFactorEnabled)
        {
            return Result<EmployeeSession>.Ok(ToSession(employee, requiresTwoFactor: true));
        }

        employee.RegisterSuccessfulLogin();
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Login succeeded for employee {EmployeeId}.", employee.Id);
        await Log(employee, AuditActions.LoginSucceeded, null, cancellationToken);
        return Result<EmployeeSession>.Ok(ToSession(employee, requiresTwoFactor: false));
    }

    internal static EmployeeSession ToSession(Employee employee, bool requiresTwoFactor) =>
        new(employee.Id, employee.FullName, employee.Code, employee.Role, employee.StockLocationId, employee.SecurityStamp, requiresTwoFactor);

    private Task Log(Employee? employee, string action, string? detail, CancellationToken cancellationToken) =>
        audit.RecordAsync(_clock, request, employee?.Id, employee?.Code, action, "employee", employee?.Id.ToString("D"), detail, cancellationToken);
}

public sealed class VerifyTwoFactorCommandHandler(
    IEmployeeRepository repository,
    ISecretProtector protector,
    ILogger<VerifyTwoFactorCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<EmployeeSession>> HandleAsync(VerifyTwoFactorCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var employee = await repository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null || !employee.IsActive || !employee.TwoFactorEnabled || employee.IsLockedOut(now))
        {
            return Result<EmployeeSession>.Fail(LoginError.InvalidCredentials);
        }

        if (string.IsNullOrWhiteSpace(command.Code))
        {
            return Result<EmployeeSession>.Fail(LoginError.InvalidCode);
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
            return Result<EmployeeSession>.Fail(LoginError.InvalidCode);
        }

        employee.RegisterSuccessfulLogin();
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Login succeeded for employee {EmployeeId} with two-factor.", employee.Id);
        if (usedRecovery)
        {
            await audit.RecordAsync(_clock, request, employee.Id, employee.Code, AuditActions.RecoveryCodeUsed, "employee", employee.Id.ToString("D"), $"{employee.RecoveryCodesLeft} left", cancellationToken);
        }

        await audit.RecordAsync(_clock, request, employee.Id, employee.Code, AuditActions.LoginSucceeded, "employee", employee.Id.ToString("D"), "2fa", cancellationToken);
        return Result<EmployeeSession>.Ok(LoginEmployeeCommandHandler.ToSession(employee, requiresTwoFactor: false));
    }
}
