using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class ConfirmTwoFactorSetupCommandHandler(
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
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
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
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
        audit.Stage(_clock, request, employee.Id, employee.Code, AuditActions.TwoFactorEnabled, "employee", employee.Id.ToString("D"), null);
        (await scope.CommitAsync(cancellationToken)).EnsureSaved();
        logger.LogInformation("Employee {EmployeeId} enabled two-factor.", employee.Id);
        return Result<TwoFactorEnabledResult, EmployeeError>.Ok(
            new TwoFactorEnabledResult(codes, LoginEmployeeCommandHandler.ToSession(employee, requiresTwoFactor: false)));
    }
}
