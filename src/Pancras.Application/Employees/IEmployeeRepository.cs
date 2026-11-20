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

    /// <summary>Giữ khóa quy tắc nhân sự (còn Admin, một Trưởng mỗi kho) đến hết scope IUnitOfWork đang mở, rồi mới đếm và đổi.</summary>
    Task LockPersonnelRulesAsync(CancellationToken cancellationToken);
}
