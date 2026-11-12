using Microsoft.Extensions.Logging;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Infrastructure.Seeding;

public sealed class AdminEmployeeSeed(
    IEmployeeRepository repository,
    IPasswordHasher passwordHasher,
    ILogger<AdminEmployeeSeed> logger)
{
    public const string Code = "admin";
    public const string Password = "Admin@12345";
    public const string FullName = "Quản trị hệ thống";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await repository.NormalizedCodeExistsAsync("ADMIN", cancellationToken))
        {
            logger.LogInformation("Admin employee is already present.");
            return;
        }

        var employee = Employee.Create(
            FullName,
            Code,
            "ADMIN",
            passwordHasher.Hash(Password),
            EmployeeRole.Admin);
        await repository.AddAsync(employee, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded admin employee {EmployeeId}.", employee.Id);
    }
}
