using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
