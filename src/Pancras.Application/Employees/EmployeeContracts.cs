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

    Task<Employee?> GetByNormalizedCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Employee?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Employee>> ListReadOnlyAsync(CancellationToken cancellationToken);

    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken);

    Task<bool> ActiveLeadExistsAsync(Guid stockLocationId, Guid? excludeEmployeeId, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Giữ khóa quy tắc nhân sự (còn Admin, một Trưởng mỗi kho) đến hết transaction, rồi mới đếm và lưu.</summary>
    Task<T> WithinPersonnelRulesAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}

public sealed record EmployeeSession(
    Guid Id,
    string FullName,
    string Code,
    string Role,
    Guid? LocationId,
    string SecurityStamp,
    bool RequiresTwoFactor = false);

public sealed record CreatedEmployee(Guid Id, string Code, string FullName, string Role);

public sealed record EmployeeProfile(
    Guid Id,
    string FullName,
    string Code,
    string Role,
    string Email,
    string Phone,
    Guid? StockLocationId,
    string Address,
    string JobTitle,
    string Department,
    bool IsActive,
    bool TwoFactorEnabled = false,
    DateTimeOffset? LockedUntil = null,
    int RecoveryCodesLeft = 0);

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

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public Guid? StockLocationId { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }
}

public sealed class UpdateEmployeeCommand
{
    public Guid Id { get; set; }

    public string? FullName { get; set; }

    public string? Password { get; set; }

    public string? Role { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public Guid? StockLocationId { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }
}

public sealed class UpdateOwnProfileCommand
{
    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }
}

public sealed class ChangeOwnPasswordCommand
{
    public string? CurrentPassword { get; set; }

    public string? NewPassword { get; set; }
}

public interface ISecretProtector
{
    string Protect(string plain);

    string? Unprotect(string protectedText);
}

public sealed record TwoFactorSetup(string Secret, string ProtectedSecret, string OtpAuthUri);

public sealed record TwoFactorEnabledResult(IReadOnlyList<string> RecoveryCodes, EmployeeSession Session);

public sealed class VerifyTwoFactorCommand
{
    public Guid EmployeeId { get; set; }

    public string? Code { get; set; }
}

public sealed class ConfirmTwoFactorCommand
{
    public string? ProtectedSecret { get; set; }

    public string? Code { get; set; }
}

public sealed class DisableTwoFactorCommand
{
    public string? CurrentPassword { get; set; }

    public string? Code { get; set; }
}
