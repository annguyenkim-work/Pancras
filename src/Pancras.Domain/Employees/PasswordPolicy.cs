namespace Pancras.Domain.Employees;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    public const int MaxLength = 128;

    public static bool IsAcceptable(string? password) =>
        password is not null && password.Length >= MinLength && password.Length <= MaxLength;
}
