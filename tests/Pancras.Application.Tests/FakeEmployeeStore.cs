using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hash:" + password;
}

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    public List<Employee> Employees { get; } = [];

    public List<Employee> Added { get; } = [];

    public int SaveCount { get; private set; }

    public int UniqueRejectionsRemaining { get; set; }

    public bool TreatAllCodesAsExisting { get; set; }

    public Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(employee => employee.NormalizedCode == normalizedCode));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(
            TreatAllCodesAsExisting
            || Employees.Any(employee => employee.NormalizedCode == normalizedCode));

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(employee => employee.Id == id));

    public Task<Employee?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Employee>> ListReadOnlyAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Employee>>(Employees.ToArray());

    public Task AddAsync(Employee employee, CancellationToken cancellationToken)
    {
        Added.Add(employee);
        Employees.Add(employee);
        return Task.CompletedTask;
    }

    public Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken)
    {
        if (UniqueRejectionsRemaining > 0)
        {
            UniqueRejectionsRemaining--;
            return Task.FromResult(false);
        }

        Added.Add(employee);
        Employees.Add(employee);
        SaveCount++;
        return Task.FromResult(true);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
