using Pancras.Application.Auditing;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class UpdateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
    ILocationRepository locations,
    IPasswordHasher passwordHasher,
    ILogger<UpdateEmployeeCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    private readonly UpdateEmployeeCommandValidator _validator = new();

    public async Task<Result<EmployeeProfile, EmployeeError>> HandleAsync(
        UpdateEmployeeCommand command,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        await repository.LockPersonnelRulesAsync(cancellationToken);
        return await SaveUpdateAsync(command, actor, scope, cancellationToken);
    }

    private async Task<Result<EmployeeProfile, EmployeeError>> SaveUpdateAsync(
        UpdateEmployeeCommand command,
        EmployeeActor actor,
        IUnitOfWorkScope scope,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            if (validation.Errors.Any(error => error.ErrorCode == nameof(EmployeeError.ProfileIncomplete)))
            {
                return Failed(EmployeeError.ProfileIncomplete);
            }

            return Failed(EmployeeError.InvalidRole);
        }

        var employee = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (employee is null)
        {
            return Failed(EmployeeError.NotFound);
        }

        if (!EmployeeAccess.CanView(actor, employee.Id, employee.StockLocationId))
        {
            return Forbidden(actor, employee.Id);
        }

        if (command.Version != employee.Version)
        {
            logger.LogInformation("Rejected a stale employee edit for {EmployeeId}.", employee.Id);
            return Failed(EmployeeError.VersionConflict);
        }

        var personalChanged =
            !Same(command.FullName, employee.FullName)
            || !Same(command.Email, employee.Email)
            || !Same(command.Phone, employee.Phone)
            || !Same(command.Address, employee.Address);
        if (personalChanged && !EmployeeAccess.CanEditPersonal(actor, employee.Id))
        {
            return Forbidden(actor, employee.Id);
        }

        var assignmentChanged =
            command.Role != employee.Role
            || !Same(command.JobTitle, employee.JobTitle)
            || !Same(command.Department, employee.Department)
            || command.StockLocationId != employee.StockLocationId;
        if (assignmentChanged)
        {
            if (!EmployeeAccess.CanEditAssignment(actor, employee.Id, employee.Role)
                || (command.Role != employee.Role && !EmployeeAccess.CanAssignRole(actor, command.Role)))
            {
                return Forbidden(actor, employee.Id);
            }

            if (employee.Role == EmployeeRole.Admin
                && command.Role != EmployeeRole.Admin
                && await repository.CountActiveAdminsAsync(cancellationToken) <= 1)
            {
                return Failed(EmployeeError.LastAdmin);
            }

            var assignment = await EmployeeRules.CheckAssignmentAsync(
                command.Role!, command.StockLocationId, employee.Id, repository, locations, cancellationToken);
            if (assignment != EmployeeError.None)
            {
                return Failed(assignment);
            }
        }

        string? newHash = null;
        if (!string.IsNullOrEmpty(command.Password))
        {
            if (!EmployeeAccess.CanResetPassword(actor))
            {
                return Forbidden(actor, employee.Id);
            }

            if (!PasswordPolicy.IsAcceptable(command.Password))
            {
                return Failed(EmployeeError.WeakPassword);
            }

            newHash = passwordHasher.Hash(command.Password);
        }

        var previousCode = employee.Code;
        if (personalChanged && !employee.Rename(command.FullName!))
        {
            return Failed(EmployeeError.ProfileIncomplete);
        }

        employee.ReplaceContact(command.Email, command.Phone, command.Address);
        if (assignmentChanged
            && !employee.ReplaceAssignment(command.Role!, command.JobTitle, command.Department, command.StockLocationId))
        {
            return Failed(EmployeeError.InvalidRole);
        }

        if (newHash is not null && !employee.ReplacePassword(newHash))
        {
            return Failed(EmployeeError.WeakPassword);
        }

        if (!string.Equals(previousCode, employee.Code, StringComparison.Ordinal))
        {
            logger.LogError("Employee {EmployeeId} code changed during rename.", employee.Id);
            return Failed(EmployeeError.ProfileIncomplete);
        }

        var employeeId = employee.Id.ToString("D");
        var action = assignmentChanged
            ? Pancras.Domain.Auditing.AuditActions.EmployeeRoleChanged
            : Pancras.Domain.Auditing.AuditActions.EmployeeUpdated;
        audit.Stage(clock ?? TimeProvider.System, request, null, null, action, "employee", employeeId, assignmentChanged ? "role/assignment changed" : null);
        if (newHash is not null)
        {
            audit.Stage(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.PasswordReset, "employee", employeeId, null);
        }

        var saved = (await scope.CommitAsync(cancellationToken)).ThrowIfFailed();
        if (saved != SaveOutcome.Saved)
        {
            logger.LogInformation("Concurrent edit rejected for employee {EmployeeId}.", employee.Id);
            return Failed(EmployeeError.VersionConflict);
        }

        logger.LogInformation("Updated employee {EmployeeId} by {ActorId}.", employee.Id, actor.Id);
        return Result<EmployeeProfile, EmployeeError>.Ok(EmployeeMapping.ToProfile(employee));
    }

    private static Result<EmployeeProfile, EmployeeError> Failed(EmployeeError error) =>
        Result<EmployeeProfile, EmployeeError>.Fail(error);

    private Result<EmployeeProfile, EmployeeError> Forbidden(EmployeeActor actor, Guid targetId)
    {
        logger.LogWarning("Employee update forbidden for {ActorId} on {EmployeeId}.", actor.Id, targetId);
        return Failed(EmployeeError.Forbidden);
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.Ordinal);
}

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(command => command.FullName)
            .Must(name => EmployeeRules.Present(name, 200))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Role)
            .Must(EmployeeRole.IsAllowed)
            .WithErrorCode(nameof(EmployeeError.InvalidRole));
        RuleFor(command => command.Email)
            .Must(EmployeeRules.IsEmail)
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Phone)
            .Must(value => EmployeeRules.Present(value, 30))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.JobTitle)
            .Must(value => EmployeeRules.Present(value, 100))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Address)
            .Must(value => EmployeeRules.Fits(value, 300))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Department)
            .Must(value => EmployeeRules.Fits(value, 100))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
    }
}

internal static class EmployeeMapping
{
    public static EmployeeProfile ToProfile(Employee employee) =>
        new(
            employee.Id,
            employee.FullName,
            employee.Code,
            employee.Role,
            employee.Email,
            employee.Phone,
            employee.StockLocationId,
            employee.Address,
            employee.JobTitle,
            employee.Department,
            employee.IsActive,
            employee.TwoFactorEnabled,
            employee.LockedUntil,
            employee.RecoveryCodesLeft,
            employee.Version);
}
