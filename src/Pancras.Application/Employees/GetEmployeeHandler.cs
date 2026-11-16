using Pancras.Application.Auditing;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class GetEmployeeHandler(
    IEmployeeRepository repository,
    ILogger<GetEmployeeHandler> logger)
{
    public async Task<Result<EmployeeProfile, EmployeeError>> HandleAsync(
        Guid id,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdReadOnlyAsync(id, cancellationToken);
        if (employee is null)
        {
            logger.LogWarning("Employee {EmployeeId} was not found.", id);
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (!EmployeeAccess.CanView(actor, employee.Id, employee.StockLocationId))
        {
            return Result<EmployeeProfile, EmployeeError>.Fail(EmployeeError.Forbidden);
        }

        return Result<EmployeeProfile, EmployeeError>.Ok(EmployeeMapping.ToProfile(employee));
    }
}
