using Microsoft.EntityFrameworkCore;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Infrastructure.Persistence;

public sealed class EmployeeRepository(PancrasDbContext context) : IEmployeeRepository
{
    /// <summary>Một khóa cho cả nhóm quy tắc nhân sự: còn ít nhất một Admin, mỗi kho một Trưởng.</summary>
    private const long PersonnelRulesLockKey = 724_531;
    public Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(employee => employee.NormalizedCode == normalizedCode, cancellationToken);

    public Task<Employee?> GetByNormalizedCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.Employees.FirstOrDefaultAsync(employee => employee.NormalizedCode == normalizedCode, cancellationToken);

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

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .CountAsync(employee => employee.Role == EmployeeRole.Admin && employee.IsActive, cancellationToken);

    public Task<bool> ActiveLeadExistsAsync(Guid stockLocationId, Guid? excludeEmployeeId, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .AnyAsync(
                employee => employee.StockLocationId == stockLocationId
                    && employee.Role == EmployeeRole.ShowroomWarehouseLead
                    && employee.IsActive
                    && (excludeEmployeeId == null || employee.Id != excludeEmployeeId),
                cancellationToken);

    public Task AddAsync(Employee employee, CancellationToken cancellationToken) =>
        context.Employees.AddAsync(employee, cancellationToken).AsTask();

    /// <summary>
    /// Khóa tư vấn (advisory lock) theo giao dịch cho nhóm quy tắc nhân sự, giữ đến hết scope IUnitOfWork: hai thao tác đồng thời
    /// cùng kiểm tra "còn ít nhất một Admin" / "mỗi kho một Trưởng" sẽ chạy lần lượt.
    /// </summary>
    public async Task LockPersonnelRulesAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Khóa quy tắc nhân sự phải chạy trong một scope IUnitOfWork đang mở.");
        }

        // pg_advisory_xact_lock returns void, so run it as a command instead of reading a result.
        await context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({PersonnelRulesLockKey})",
            cancellationToken);
    }
}
