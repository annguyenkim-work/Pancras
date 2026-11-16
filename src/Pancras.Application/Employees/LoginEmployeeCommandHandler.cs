using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

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

    public async Task<Result<EmployeeSession, LoginError>> HandleAsync(
        LoginEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        if (!_validator.Validate(command).IsValid)
        {
            logger.LogInformation("Login rejected because a field was empty.");
            return Result<EmployeeSession, LoginError>.Fail(LoginError.MissingCredentials);
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
            return Result<EmployeeSession, LoginError>.Fail(LoginError.InvalidCredentials);
        }

        if (!passwordHasher.Verify(command.Password!, employee.PasswordHash))
        {
            var locked = employee.RegisterFailedLogin(now);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Login rejected because the credentials did not match.");
            await Log(employee, locked ? AuditActions.LoginLockedOut : AuditActions.LoginFailed, "wrong password", cancellationToken);
            return Result<EmployeeSession, LoginError>.Fail(LoginError.InvalidCredentials);
        }

        if (employee.TwoFactorEnabled)
        {
            return Result<EmployeeSession, LoginError>.Ok(ToSession(employee, requiresTwoFactor: true));
        }

        employee.RegisterSuccessfulLogin();
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Login succeeded for employee {EmployeeId}.", employee.Id);
        await Log(employee, AuditActions.LoginSucceeded, null, cancellationToken);
        return Result<EmployeeSession, LoginError>.Ok(ToSession(employee, requiresTwoFactor: false));
    }

    internal static EmployeeSession ToSession(Employee employee, bool requiresTwoFactor) =>
        new(employee.Id, employee.FullName, employee.Code, employee.Role, employee.StockLocationId, employee.SecurityStamp, requiresTwoFactor);

    private Task Log(Employee? employee, string action, string? detail, CancellationToken cancellationToken) =>
        audit.RecordAsync(_clock, request, employee?.Id, employee?.Code, action, "employee", employee?.Id.ToString("D"), detail, cancellationToken);
}

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
