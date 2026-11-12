using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public static class EmployeeRoles
{
    public static IReadOnlyList<string> All => EmployeeRole.All;
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public interface IEmployeeRepository
{
    Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Employee?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record EmployeeSession(Guid Id, string FullName, string Code, string Role, string SecurityStamp);

public sealed record CreatedEmployee(Guid Id, string Code, string FullName, string Role);

public sealed record EmployeeProfile(Guid Id, string FullName, string Code, string Role);

public sealed class LoginEmployeeCommand
{
    public string? EmployeeCode { get; set; }

    public string? Password { get; set; }
}

public sealed class CreateEmployeeCommand
{
    public string? FullName { get; set; }

    public string? Password { get; set; }

    public string? Role { get; set; }
}

public sealed class UpdateEmployeeCommand
{
    public Guid Id { get; set; }

    public string? FullName { get; set; }

    public string? Password { get; set; }

    public string? Role { get; set; }
}
