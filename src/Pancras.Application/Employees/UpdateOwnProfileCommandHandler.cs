using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class UpdateOwnProfileCommandHandler(
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateOwnProfileCommandHandler> logger)
{
    public async Task<Result<EmployeeSession, EmployeeError>> HandleAsync(
        UpdateOwnProfileCommand command,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (!EmployeeRules.Present(command.FullName, 200)
            || !EmployeeRules.IsEmail(command.Email)
            || !EmployeeRules.Present(command.Phone, 30)
            || !EmployeeRules.Fits(command.Address, 300))
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.ProfileIncomplete);
        }

        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var employee = await repository.GetByIdAsync(actor.Id, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (!employee.Rename(command.FullName!))
        {
            return Result<EmployeeSession, EmployeeError>.Fail(EmployeeError.ProfileIncomplete);
        }

        employee.ReplaceContact(command.Email, command.Phone, command.Address);
        (await scope.CommitAsync(cancellationToken)).EnsureSaved();
        logger.LogInformation("Employee {EmployeeId} updated their own profile.", employee.Id);
        return Result<EmployeeSession, EmployeeError>.Ok(
            new EmployeeSession(employee.Id, employee.FullName, employee.Code, employee.Role, employee.StockLocationId, employee.SecurityStamp));
    }
}
