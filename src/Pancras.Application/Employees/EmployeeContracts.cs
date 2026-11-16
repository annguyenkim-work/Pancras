using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public static class EmployeeRoles
{
    public static IReadOnlyList<string> All => EmployeeRole.All;
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
    int RecoveryCodesLeft = 0,
    int Version = 0);

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

    /// <summary>Phiên bản hồ sơ lúc mở form; khác phiên bản hiện tại nghĩa là có người sửa trước.</summary>
    public int Version { get; set; }

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
