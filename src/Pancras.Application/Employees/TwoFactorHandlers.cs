using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class BeginTwoFactorSetupHandler(
    IEmployeeRepository repository,
    ISecretProtector protector)
{
    public const string Issuer = "PancrasERP";

    public async Task<Result<TwoFactorSetup, EmployeeError>> HandleAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdReadOnlyAsync(actor.Id, cancellationToken);
        if (employee is null)
        {
            return Result<TwoFactorSetup, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (employee.TwoFactorEnabled)
        {
            return Result<TwoFactorSetup, EmployeeError>.Fail(EmployeeError.TwoFactorAlreadyEnabled);
        }

        var secret = Totp.ToBase32(Totp.NewSecret());
        return Result<TwoFactorSetup, EmployeeError>.Ok(
            new TwoFactorSetup(secret, protector.Protect(secret), Totp.BuildUri(Issuer, employee.Code, secret)));
    }
}

public sealed class ConfirmTwoFactorSetupCommandHandler(
    IEmployeeRepository repository,
    ISecretProtector protector,
    ILogger<ConfirmTwoFactorSetupCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<TwoFactorEnabledResult, EmployeeError>> HandleAsync(
        ConfirmTwoFactorCommand command,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(actor.Id, cancellationToken);
        if (employee is null)
        {
            return Result<TwoFactorEnabledResult, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (employee.TwoFactorEnabled)
        {
            return Result<TwoFactorEnabledResult, EmployeeError>.Fail(EmployeeError.TwoFactorAlreadyEnabled);
        }

        var secretText = string.IsNullOrEmpty(command.ProtectedSecret) ? null : protector.Unprotect(command.ProtectedSecret);
        var secret = Totp.FromBase32(secretText);
        var step = secret is null ? null : Totp.Verify(secret, command.Code, _clock.GetUtcNow(), 0);
        if (step is null)
        {
            logger.LogInformation("Two-factor setup rejected for employee {EmployeeId}.", employee.Id);
            return Result<TwoFactorEnabledResult, EmployeeError>.Fail(EmployeeError.InvalidCode);
        }

        var codes = RecoveryCodes.Generate();
        employee.EnableTwoFactor(command.ProtectedSecret!, codes.Select(RecoveryCodes.Hash), step.Value);
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Employee {EmployeeId} enabled two-factor.", employee.Id);
        await audit.RecordAsync(_clock, request, employee.Id, employee.Code, AuditActions.TwoFactorEnabled, "employee", employee.Id.ToString("D"), null, cancellationToken);
        return Result<TwoFactorEnabledResult, EmployeeError>.Ok(
            new TwoFactorEnabledResult(codes, LoginEmployeeCommandHandler.ToSession(employee, requiresTwoFactor: false)));
    }
}

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

public sealed class UnlockEmployeeCommandHandler(
    IEmployeeRepository repository,
    ILogger<UnlockEmployeeCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<EmployeeProfile, EmployeeError>> HandleAsync(Guid employeeId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!EmployeeAccess.CanResetPassword(actor))
        {
            logger.LogWarning("Unlock forbidden for employee {EmployeeId}.", actor.Id);
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.Forbidden);
        }

        var employee = await repository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        employee.Unlock();
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Employee {EmployeeId} was unlocked by {ActorId}.", employee.Id, actor.Id);
        await audit.RecordAsync(_clock, request, actor.Id, null, AuditActions.EmployeeUnlocked, "employee", employee.Id.ToString("D"), null, cancellationToken);
        return Result<EmployeeProfile, EmployeeError>.Ok(EmployeeMapping.ToProfile(employee));
    }
}
