using Microsoft.Extensions.Logging;

namespace Pancras.Application.Employees;

public sealed record EmployeeCard(
    Guid Id,
    string FullName,
    string Code,
    string Role,
    string Email,
    string Phone,
    string Branch,
    string JobTitle,
    string Department);

public sealed class ListEmployeesHandler(
    IEmployeeRepository repository,
    ILogger<ListEmployeesHandler> logger)
{
    public const int PageSize = 20;

    public async Task<IReadOnlyList<EmployeeCard>> HandleAsync(
        string? name,
        string? code,
        string? role,
        string? branch,
        CancellationToken cancellationToken)
    {
        var employees = await repository.ListReadOnlyAsync(cancellationToken);
        var nameTerm = name?.Trim();
        var codeTerm = code?.Trim();
        var selectedRole = role?.Trim();
        var branchTerm = branch?.Trim();
        var cards = employees
            .Where(employee =>
                (string.IsNullOrEmpty(nameTerm) || employee.FullName.Contains(nameTerm, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrEmpty(codeTerm) || employee.Code.Contains(codeTerm, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrEmpty(selectedRole) || employee.Role == selectedRole)
                && (string.IsNullOrEmpty(branchTerm) || employee.Branch.Contains(branchTerm, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(employee => new EmployeeCard(
                employee.Id,
                employee.FullName,
                employee.Code,
                employee.Role,
                employee.Email,
                employee.Phone,
                employee.Branch,
                employee.JobTitle,
                employee.Department))
            .ToArray();
        logger.LogInformation("Listed {EmployeeCount} employee cards.", cards.Length);
        return cards;
    }
}
