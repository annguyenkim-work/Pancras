using Microsoft.AspNetCore.Identity;
using Pancras.Application.Employees;

namespace Pancras.Infrastructure.Identity;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string password, string passwordHash) =>
        _hasher.VerifyHashedPassword(new object(), passwordHash, password) != PasswordVerificationResult.Failed;
}
