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
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; }

    public string Code { get; private set; }

    public string NormalizedCode { get; private set; }

    public string PasswordHash { get; private set; }

    public string Role { get; private set; }

    public string SecurityStamp { get; private set; }

    public static Employee Create(
        string fullName,
        string code,
        string normalizedCode,
        string passwordHash,
        string role)
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
            SecurityStamp = NewSecurityStamp()
        };
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
