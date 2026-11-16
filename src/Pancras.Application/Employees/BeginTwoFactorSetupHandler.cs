using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public sealed class BeginTwoFactorSetupHandler(
    IEmployeeRepository repository,
    ISecretProtector protector)
{
    public const string Issuer = "PancrasERP";

    public async Task<Result<TwoFactorSetup, EmployeeError>> HandleAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdReadOnlyAsync(actor.Id, cancellationToken);
        if (employee is null)
        {
            return Result<TwoFactorSetup, EmployeeError>.Fail(EmployeeError.NotFound);
        }

        if (employee.TwoFactorEnabled)
        {
            return Result<TwoFactorSetup, EmployeeError>.Fail(EmployeeError.TwoFactorAlreadyEnabled);
        }

        var secret = Totp.ToBase32(Totp.NewSecret());
        return Result<TwoFactorSetup, EmployeeError>.Ok(
            new TwoFactorSetup(secret, protector.Protect(secret), Totp.BuildUri(Issuer, employee.Code, secret)));
    }
}
