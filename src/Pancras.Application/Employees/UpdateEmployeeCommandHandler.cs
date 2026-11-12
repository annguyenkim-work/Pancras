using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(command => command.FullName)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200)
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Role)
            .Must(EmployeeRole.IsAllowed)
            .WithErrorCode(nameof(EmployeeError.InvalidRole));
    }
}

public sealed class UpdateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IPasswordHasher passwordHasher,
    ILogger<UpdateEmployeeCommandHandler> logger)
{
    private readonly UpdateEmployeeCommandValidator _validator = new();

    public async Task<Result<EmployeeProfile>> HandleAsync(
        UpdateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            if (validation.Errors.Any(error => error.ErrorCode == nameof(EmployeeError.ProfileIncomplete)))
            {
                return Result<EmployeeProfile>.Fail(EmployeeError.ProfileIncomplete);
            }

            return Result<EmployeeProfile>.Fail(EmployeeError.InvalidRole);
        }

        var employee = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeProfile>.Fail(EmployeeError.NotFound);
        }

        var previousCode = employee.Code;
        if (!employee.Rename(command.FullName!) || !employee.ChangeRole(command.Role!))
        {
            return Result<EmployeeProfile>.Fail(EmployeeError.ProfileIncomplete);
        }

        if (!string.IsNullOrEmpty(command.Password)
            && !employee.ReplacePassword(passwordHasher.Hash(command.Password)))
        {
            return Result<EmployeeProfile>.Fail(EmployeeError.ProfileIncomplete);
        }

        if (!string.Equals(previousCode, employee.Code, StringComparison.Ordinal))
        {
            logger.LogError("Employee {EmployeeId} code changed during rename.", employee.Id);
            return Result<EmployeeProfile>.Fail(EmployeeError.ProfileIncomplete);
        }

        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated employee {EmployeeId}.", employee.Id);
        return Result<EmployeeProfile>.Ok(
            new EmployeeProfile(employee.Id, employee.FullName, employee.Code, employee.Role));
    }
}

public sealed class GetEmployeeHandler(
    IEmployeeRepository repository,
    ILogger<GetEmployeeHandler> logger)
{
    public async Task<Result<EmployeeProfile>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdReadOnlyAsync(id, cancellationToken);
        if (employee is null)
        {
            logger.LogWarning("Employee {EmployeeId} was not found.", id);
            return Result<EmployeeProfile>.Fail(EmployeeError.NotFound);
        }

        logger.LogInformation("Loaded employee {EmployeeId}.", employee.Id);

        return Result<EmployeeProfile>.Ok(
            new EmployeeProfile(employee.Id, employee.FullName, employee.Code, employee.Role));
    }
}
