using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(command => command.FullName)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200)
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Password)
            .Must(password => !string.IsNullOrEmpty(password))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Role)
            .Must(EmployeeRole.IsAllowed)
            .WithErrorCode(nameof(EmployeeError.InvalidRole));
    }
}

public sealed class CreateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IPasswordHasher passwordHasher,
    ILogger<CreateEmployeeCommandHandler> logger)
{
    private readonly CreateEmployeeCommandValidator _validator = new();

    public async Task<Result<CreatedEmployee>> HandleAsync(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            if (validation.Errors.Any(error => error.ErrorCode == nameof(EmployeeError.ProfileIncomplete)))
            {
                return Result<CreatedEmployee>.Fail(EmployeeError.ProfileIncomplete);
            }

            return Result<CreatedEmployee>.Fail(EmployeeError.InvalidRole);
        }

        var fullName = command.FullName!.Trim();
        if (!EmployeeCodeGenerator.HasUsableName(fullName))
        {
            logger.LogInformation("Rejected an employee name that cannot produce a code.");
            return Result<CreatedEmployee>.Fail(EmployeeError.NameUnusable);
        }

        var passwordHash = passwordHasher.Hash(command.Password!);
        foreach (var attempt in EmployeeCodeGenerator.Attempts(fullName))
        {
            if (await repository.NormalizedCodeExistsAsync(attempt.NormalizedCode, cancellationToken))
            {
                continue;
            }

            var employee = Employee.Create(
                fullName,
                attempt.Code,
                attempt.NormalizedCode,
                passwordHash,
                command.Role!);
            if (!await repository.TryAddAsync(employee, cancellationToken))
            {
                continue;
            }

            logger.LogInformation("Created employee {EmployeeId}.", employee.Id);
            return Result<CreatedEmployee>.Ok(
                new CreatedEmployee(employee.Id, employee.Code, employee.FullName, employee.Role));
        }

        logger.LogWarning("Could not allocate an employee code.");
        return Result<CreatedEmployee>.Fail(EmployeeError.CodeExhausted);
    }
}
