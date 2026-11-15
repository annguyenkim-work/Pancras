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
    private readonly SemaphoreSlim _rules = new(1, 1);
    private int _adminCounts;
    private TaskCompletionSource<bool>? _firstAdminCounted;
    private TaskCompletionSource<bool>? _releaseFirstAdminCount;

    public List<Employee> Employees { get; } = [];

    public int AdminCounts => _adminCounts;

    public void GateFirstAdminCount(TaskCompletionSource<bool> counted, TaskCompletionSource<bool> release)
    {
        _firstAdminCounted = counted;
        _releaseFirstAdminCount = release;
    }

    public List<Employee> Added { get; } = [];

    public int SaveCount { get; private set; }

    public int UniqueRejectionsRemaining { get; set; }

    public bool TreatAllCodesAsExisting { get; set; }

    public Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(employee => employee.NormalizedCode == normalizedCode));

    public Task<Employee?> GetByNormalizedCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken) =>
        GetByNormalizedCodeAsync(normalizedCode, cancellationToken);

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

    public async Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken)
    {
        var count = Employees.Count(employee => employee.Role == EmployeeRole.Admin && employee.IsActive);
        var call = Interlocked.Increment(ref _adminCounts);
        if (call == 1 && _releaseFirstAdminCount is not null)
        {
            _firstAdminCounted?.TrySetResult(true);
            await _releaseFirstAdminCount.Task.WaitAsync(cancellationToken);
        }

        return count;
    }

    public Task<bool> ActiveLeadExistsAsync(Guid stockLocationId, Guid? excludeEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.Any(employee =>
            employee.StockLocationId == stockLocationId
            && employee.Role == EmployeeRole.ShowroomWarehouseLead
            && employee.IsActive
            && employee.Id != excludeEmployeeId));

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

    public async Task<T> WithinPersonnelRulesAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await _rules.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            _rules.Release();
        }
    }
}

internal sealed class FakeLocationRepository : Pancras.Application.Locations.ILocationRepository
{
    public List<Pancras.Domain.Inventory.StockLocation> Locations { get; } = [];

    public Task<IReadOnlyList<Pancras.Domain.Inventory.StockLocation>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Pancras.Domain.Inventory.StockLocation>>(Locations.ToArray());

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Locations.Any(location => location.Id == id));

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Locations.Any(location => string.Equals(location.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> TryAddAsync(Pancras.Domain.Inventory.StockLocation location, CancellationToken cancellationToken)
    {
        Locations.Add(location);
        return Task.FromResult(true);
    }
}
