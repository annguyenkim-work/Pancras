using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;

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
    ILogger<LoginEmployeeCommandHandler> logger)
{
    private readonly LoginEmployeeCommandValidator _validator = new();

    public async Task<Result<EmployeeSession>> HandleAsync(
        LoginEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        if (!_validator.Validate(command).IsValid)
        {
            logger.LogInformation("Login rejected because a field was empty.");
            return Result<EmployeeSession>.Fail(LoginError.MissingCredentials);
        }

        var normalizedCode = command.EmployeeCode!.Trim().ToUpperInvariant();
        var employee = await repository.GetByNormalizedCodeAsync(normalizedCode, cancellationToken);
        if (employee is null || !passwordHasher.Verify(command.Password!, employee.PasswordHash))
        {
            logger.LogInformation("Login rejected because the credentials did not match.");
            return Result<EmployeeSession>.Fail(LoginError.InvalidCredentials);
        }

        logger.LogInformation("Login succeeded for employee {EmployeeId}.", employee.Id);
        return Result<EmployeeSession>.Ok(
            new EmployeeSession(
                employee.Id,
                employee.FullName,
                employee.Code,
                employee.Role,
                employee.SecurityStamp));
    }
}
