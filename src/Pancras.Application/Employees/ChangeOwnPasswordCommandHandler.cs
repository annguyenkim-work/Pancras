using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class ChangeOwnPasswordCommandHandler(
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ILogger<ChangeOwnPasswordCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    public async Task<Result<EmployeeSession, EmployeeError>> HandleAsync(
        ChangeOwnPasswordCommand command,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var employee = await repository.GetByIdAsync(actor.Id, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (string.IsNullOrEmpty(command.CurrentPassword)
            || !passwordHasher.Verify(command.CurrentPassword, employee.PasswordHash))
        {
            logger.LogInformation("Password change rejected for employee {EmployeeId}.", employee.Id);
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.WrongPassword);
        }

        if (!PasswordPolicy.IsAcceptable(command.NewPassword))
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.WeakPassword);
        }

        employee.ReplacePassword(passwordHasher.Hash(command.NewPassword!));
        audit.Stage(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.PasswordChanged, "employee", employee.Id.ToString("D"), null);
        (await scope.CommitAsync(cancellationToken)).EnsureSaved();
        logger.LogInformation("Employee {EmployeeId} changed their password.", employee.Id);
        return Result<EmployeeSession, EmployeeError>.Ok(
            new EmployeeSession(employee.Id, employee.FullName, employee.Code, employee.Role, employee.StockLocationId, employee.SecurityStamp));
    }
}
