using Pancras.Domain.Employees;

namespace Pancras.Application.Employees;

public interface IEmployeeRepository
{
    Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<Employee?> GetByNormalizedCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Employee?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Employee>> ListReadOnlyAsync(CancellationToken cancellationToken);

    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken);

    Task<bool> ActiveLeadExistsAsync(Guid stockLocationId, Guid? excludeEmployeeId, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Giữ khóa quy tắc nhân sự (còn Admin, một Trưởng mỗi kho) đến hết transaction, rồi mới đếm và lưu.</summary>
    Task<T> WithinPersonnelRulesAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
