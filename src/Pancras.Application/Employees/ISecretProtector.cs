using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public interface ISecretProtector
{
    string Protect(string plain);

    string? Unprotect(string protectedText);
}
