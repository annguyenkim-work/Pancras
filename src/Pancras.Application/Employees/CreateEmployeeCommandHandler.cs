using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

internal static class EmployeeRules
{
    public static bool IsEmail(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        var at = text.IndexOf('@');
        return at > 0 && text.LastIndexOf('.') > at + 1 && text[^1] != '.' && !text.Contains(' ');
    }

    public static bool Present(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    public static bool Fits(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maxLength;

    /// <summary>Kiểm tra kho gắn với vai trò: bắt buộc với Trưởng kho, Thủ kho, Sales; mỗi kho tối đa một Trưởng.</summary>
    public static async Task<EmployeeError> CheckAssignmentAsync(
        string role,
        Guid? locationId,
        Guid? employeeId,
        IEmployeeRepository employees,
        ILocationRepository locations,
        CancellationToken cancellationToken)
    {
        if (EmployeeRole.RequiresLocation(role) && locationId is null)
        {
            return EmployeeError.LocationRequired;
        }

        if (locationId is Guid id)
        {
            if (!await locations.ExistsAsync(id, cancellationToken))
            {
                return EmployeeError.LocationNotFound;
            }

            if (role == EmployeeRole.ShowroomWarehouseLead
                && await employees.ActiveLeadExistsAsync(id, employeeId, cancellationToken))
            {
                return EmployeeError.LeadExists;
            }
        }

        return EmployeeError.None;
    }
}

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(command => command.FullName)
            .Must(name => EmployeeRules.Present(name, 200))
            .WithErrorCode(nameof(EmployeeError.ProfileIncomplete));
        RuleFor(command => command.Password)
            .Must(PasswordPolicy.IsAcceptable)
            .WithErrorCode(nameof(EmployeeError.WeakPassword));
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

public sealed class CreateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
    ILocationRepository locations,
    IPasswordHasher passwordHasher,
    ILogger<CreateEmployeeCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    private readonly CreateEmployeeCommandValidator _validator = new();

    public async Task<Result<CreatedEmployee, EmployeeError>> HandleAsync(
        CreateEmployeeCommand command,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Role))
        {
            return Result<CreatedEmployee, EmployeeError>.Fail(EmployeeError.RoleRequired);
        }

        if (!EmployeeAccess.CanCreate(actor, command.Role, command.StockLocationId))
        {
            logger.LogWarning("Employee creation forbidden for employee {EmployeeId}.", actor.Id);
            return Result<CreatedEmployee, EmployeeError>.Fail(EmployeeError.Forbidden);
        }

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            if (validation.Errors.Any(error => error.ErrorCode == nameof(EmployeeError.WeakPassword)))
            {
                return Result<CreatedEmployee, EmployeeError>.Fail(EmployeeError.WeakPassword);
            }

            if (validation.Errors.Any(error => error.ErrorCode == nameof(EmployeeError.ProfileIncomplete)))
            {
                return Result<CreatedEmployee, EmployeeError>.Fail(EmployeeError.ProfileIncomplete);
            }

            return Result<CreatedEmployee, EmployeeError>.Fail(EmployeeError.InvalidRole);
        }

        var assignment = await EmployeeRules.CheckAssignmentAsync(
            command.Role!, command.StockLocationId, null, repository, locations, cancellationToken);
        if (assignment != EmployeeError.None)
        {
            return Result<CreatedEmployee, EmployeeError>.Fail(assignment);
        }

        var fullName = command.FullName!.Trim();
        if (!EmployeeCodeGenerator.HasUsableName(fullName))
        {
            logger.LogInformation("Rejected an employee name that cannot produce a code.");
            return Result<CreatedEmployee, EmployeeError>.Fail(EmployeeError.NameUnusable);
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
                command.Role!,
                command.Email,
                command.Phone,
                command.StockLocationId,
                command.JobTitle,
                command.Department,
                command.Address);
            await using var scope = await unitOfWork.BeginAsync(cancellationToken);
            await repository.AddAsync(employee, cancellationToken);
            audit.Stage(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.EmployeeCreated, "employee", employee.Id.ToString("D"), $"role={employee.Role}");
            if ((await scope.CommitAsync(cancellationToken)).ThrowIfFailed() != SaveOutcome.Saved)
            {
                // Trùng mã (có người vừa lấy mã này): thử mã kế tiếp trong một giao dịch mới.
                continue;
            }

            logger.LogInformation("Created employee {EmployeeId} by {ActorId}.", employee.Id, actor.Id);
            return Result<CreatedEmployee, EmployeeError>.Ok(
                new CreatedEmployee(employee.Id, employee.Code, employee.FullName, employee.Role));
        }

        logger.LogWarning("Could not allocate an employee code.");
        return Result<CreatedEmployee, EmployeeError>.Fail(EmployeeError.CodeExhausted);
    }
}
