namespace Pancras.Domain.Employees;

public sealed class Employee
{
    private Employee()
    {
        FullName = string.Empty;
        Code = string.Empty;
        NormalizedCode = string.Empty;
        PasswordHash = string.Empty;
        Role = string.Empty;
        SecurityStamp = string.Empty;
        Email = string.Empty;
        Phone = string.Empty;
        Address = string.Empty;
        IsActive = true;
        TotpSecret = string.Empty;
        RecoveryCodeHashes = string.Empty;
        JobTitle = string.Empty;
        Department = string.Empty;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; }

    public string Code { get; private set; }

    public string NormalizedCode { get; private set; }

    public string PasswordHash { get; private set; }

    public string Role { get; private set; }

    public string SecurityStamp { get; private set; }

    public string Email { get; private set; }

    public string Phone { get; private set; }

    public Guid? StockLocationId { get; private set; }

    public string Address { get; private set; }

    public bool IsActive { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public bool TwoFactorEnabled { get; private set; }

    /// <summary>Khóa TOTP đã mã hóa bằng Data Protection. Không bao giờ lưu dạng thô.</summary>
    public string TotpSecret { get; private set; }

    /// <summary>Hash SHA-256 của các mã khôi phục chưa dùng, ngăn bằng dấu phẩy.</summary>
    public string RecoveryCodeHashes { get; private set; }

    public long LastTotpStep { get; private set; }

    public string JobTitle { get; private set; }

    public string Department { get; private set; }

    public static Employee Create(
        string fullName,
        string code,
        string normalizedCode,
        string passwordHash,
        string role,
        string? email = null,
        string? phone = null,
        Guid? stockLocationId = null,
        string? jobTitle = null,
        string? department = null,
        string? address = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name is required.", nameof(fullName));
        }

        var trimmedName = fullName.Trim();
        if (trimmedName.Length > 200)
        {
            throw new ArgumentException("Full name is too long.", nameof(fullName));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new ArgumentException("Employee code is required.", nameof(code));
        }

        if (string.IsNullOrEmpty(passwordHash) || passwordHash.Length > 500)
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        if (!EmployeeRole.IsAllowed(role))
        {
            throw new ArgumentException("Role is not allowed.", nameof(role));
        }

        return new Employee
        {
            Id = Guid.CreateVersion7(),
            FullName = trimmedName,
            Code = code,
            NormalizedCode = normalizedCode,
            PasswordHash = passwordHash,
            Role = role,
            SecurityStamp = NewSecurityStamp(),
            Email = Fit(email, 200),
            Phone = Fit(phone, 30),
            StockLocationId = stockLocationId,
            Address = Fit(address, 300),
            JobTitle = Fit(jobTitle, 100),
            Department = Fit(department, 100)
        };
    }

    public void ReplaceContact(string? email, string? phone, string? address)
    {
        Email = Fit(email, 200);
        Phone = Fit(phone, 30);
        Address = Fit(address, 300);
    }

    public bool ReplaceAssignment(string role, string? jobTitle, string? department, Guid? stockLocationId)
    {
        if (!ChangeRole(role))
        {
            return false;
        }

        JobTitle = Fit(jobTitle, 100);
        Department = Fit(department, 100);
        if (StockLocationId != stockLocationId)
        {
            StockLocationId = stockLocationId;
            SecurityStamp = NewSecurityStamp();
        }

        return true;
    }

    public const int MaxFailedLogins = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil is DateTimeOffset until && until > now;

    /// <summary>Ghi nhận một lần sai. Trả về true nếu tài khoản vừa bị khóa.</summary>
    public bool RegisterFailedLogin(DateTimeOffset now)
    {
        if (LockedUntil is DateTimeOffset until && until <= now)
        {
            LockedUntil = null;
            FailedLoginCount = 0;
        }

        FailedLoginCount++;
        if (FailedLoginCount < MaxFailedLogins)
        {
            return false;
        }

        FailedLoginCount = 0;
        LockedUntil = now + LockoutDuration;
        return true;
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public void Unlock() => RegisterSuccessfulLogin();

    public void EnableTwoFactor(string protectedSecret, IEnumerable<string> recoveryCodeHashes, long usedStep)
    {
        TotpSecret = protectedSecret;
        RecoveryCodeHashes = string.Join(',', recoveryCodeHashes);
        LastTotpStep = usedStep;
        TwoFactorEnabled = true;
        SecurityStamp = NewSecurityStamp();
    }

    public void DisableTwoFactor()
    {
        TwoFactorEnabled = false;
        TotpSecret = string.Empty;
        RecoveryCodeHashes = string.Empty;
        LastTotpStep = 0;
        SecurityStamp = NewSecurityStamp();
    }

    public bool TryUseTotpStep(long step)
    {
        if (step <= LastTotpStep)
        {
            return false;
        }

        LastTotpStep = step;
        return true;
    }

    public bool TryUseRecoveryCode(string code)
    {
        var hash = RecoveryCodes.Hash(code);
        var hashes = RecoveryCodeHashes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!hashes.Remove(hash))
        {
            return false;
        }

        RecoveryCodeHashes = string.Join(',', hashes);
        return true;
    }

    public int RecoveryCodesLeft =>
        RecoveryCodeHashes.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        SecurityStamp = NewSecurityStamp();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        // Kích hoạt lại KHÔNG đụng tới 2FA hay khóa đăng nhập. Giám đốc được phép ngưng rồi kích hoạt lại nhân viên,
        // nhưng quy tắc là chỉ Admin được tắt/reset 2FA và mở khóa (ResetTwoFactorCommandHandler, UnlockEmployeeCommandHandler).
        // Nếu Activate xóa chúng, Giám đốc có thể vòng qua quy tắc đó bằng cách ngưng rồi kích hoạt lại.
        // Cờ 2FA, khóa bí mật, mã khôi phục và bước TOTP luôn đi cùng nhau, nên không xóa một phần nào ở đây.
        IsActive = true;
        SecurityStamp = NewSecurityStamp();
    }

    private static string Fit(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public bool Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return false;
        }

        var trimmedName = fullName.Trim();
        if (trimmedName.Length > 200)
        {
            return false;
        }

        FullName = trimmedName;
        return true;
    }

    public bool ReplacePassword(string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash) || passwordHash.Length > 500)
        {
            return false;
        }

        PasswordHash = passwordHash;
        SecurityStamp = NewSecurityStamp();
        return true;
    }

    public bool ChangeRole(string role)
    {
        if (!EmployeeRole.IsAllowed(role))
        {
            return false;
        }

        if (string.Equals(Role, role, StringComparison.Ordinal))
        {
            return true;
        }

        Role = role;
        SecurityStamp = NewSecurityStamp();
        return true;
    }

    private static string NewSecurityStamp() => Guid.CreateVersion7().ToString("N");
}
