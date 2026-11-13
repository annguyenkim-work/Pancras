using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Infrastructure.Persistence;

public sealed class EmployeeRepository(PancrasDbContext context) : IEmployeeRepository
{
    public Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(employee => employee.NormalizedCode == normalizedCode, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .AnyAsync(employee => employee.NormalizedCode == normalizedCode, cancellationToken);

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public Task<Employee?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Employee>> ListReadOnlyAsync(CancellationToken cancellationToken) =>
        await context.Employees.AsNoTracking()
            .OrderBy(employee => employee.FullName)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken) =>
        await context.Employees.AddAsync(employee, cancellationToken);

    public async Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken)
    {
        await context.Employees.AddAsync(employee, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.Entry(employee).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
