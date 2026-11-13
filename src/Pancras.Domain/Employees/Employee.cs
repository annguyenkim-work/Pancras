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
        Branch = string.Empty;
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

    public string Branch { get; private set; }

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
        string? branch = null,
        string? jobTitle = null,
        string? department = null)
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
            Branch = Fit(branch, 200),
            JobTitle = Fit(jobTitle, 100),
            Department = Fit(department, 100)
        };
    }

    public bool ReplaceProfile(string? email, string? phone, string? branch, string? jobTitle, string? department)
    {
        Email = Fit(email, 200);
        Phone = Fit(phone, 30);
        Branch = Fit(branch, 200);
        JobTitle = Fit(jobTitle, 100);
        Department = Fit(department, 100);
        return true;
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
