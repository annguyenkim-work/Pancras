using Pancras.Application.Common;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

internal static class SupplierActors
{
    public static EmployeeActor Admin { get; } = new(Guid.CreateVersion7(), EmployeeRole.Admin, null);

    public static EmployeeActor Director { get; } = new(Guid.CreateVersion7(), EmployeeRole.Director, null);

    public static EmployeeActor Accountant { get; } = new(Guid.CreateVersion7(), EmployeeRole.Accountant, null);

    public static EmployeeActor Keeper { get; } = new(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, Guid.CreateVersion7());

    public static EmployeeActor Sales { get; } = new(Guid.CreateVersion7(), EmployeeRole.Sales, Guid.CreateVersion7());
}

internal sealed class FakeSupplierRepository : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public bool RejectNextSave { get; set; }

    public SaveOutcome? NextOutcome { get; set; }

    public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Supplier>>(Filtered(SupplierListFilter.From(null)));

    public Task<SupplierSlice> ListFilteredAsync(
        SupplierListFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var ordered = SupplierOrdering.Apply(Filtered(filter).AsQueryable(), filter.Order).ToList();
        var size = pageSize < 1 ? SupplierPageRequest.DefaultSize : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<Supplier> items = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new SupplierSlice(items, ordered.Count, number));
    }

    public Task<IReadOnlyList<string>> ListCountriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Suppliers.Select(supplier => supplier.Country).Distinct().Order().ToList());

    private List<Supplier> Filtered(SupplierListFilter filter)
    {
        IEnumerable<Supplier> suppliers = filter.Status switch
        {
            SupplierStatusFilter.Inactive => Suppliers.Where(supplier => !supplier.IsActive),
            SupplierStatusFilter.All => Suppliers,
            _ => Suppliers.Where(supplier => supplier.IsActive)
        };

        if (filter.Country is not null)
        {
            suppliers = suppliers.Where(supplier => supplier.Country == filter.Country);
        }

        if (filter.Term is not null)
        {
            var needle = filter.Term;
            suppliers = suppliers.Where(supplier =>
                supplier.Code.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.ContactName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.Email.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.Phone.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.Country.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        return suppliers.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Code).ToList();
    }

    public Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id && supplier.IsActive));

    public Task<Supplier?> GetAnyReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id));

    public Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetActiveReadOnlyAsync(id, cancellationToken);

    public Task<Supplier?> GetAnyForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetAnyReadOnlyAsync(id, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Suppliers.Where(supplier => ids.Contains(supplier.Id)).ToDictionary(supplier => supplier.Id, supplier => supplier.Name));

    public Task<bool> NormalizedCodeExistsAsync(
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Any(supplier =>
            supplier.IsActive
            && supplier.NormalizedCode == normalizedCode
            && supplier.Id != excludingId));

    public Task<string?> InactiveNameWithCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => !supplier.IsActive && supplier.NormalizedCode == normalizedCode)?.Name);

    public Task<bool> TryAddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        if (Suppliers.Any(existing => existing.IsActive && existing.NormalizedCode == supplier.NormalizedCode))
        {
            return Task.FromResult(false);
        }

        Suppliers.Add(supplier);
        return Task.FromResult(true);
    }

    public Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken)
    {
        var count = Suppliers.Count(supplier =>
            supplier.IsActive
            && (string.CompareOrdinal(supplier.Name, name) < 0
                || (supplier.Name == name && string.CompareOrdinal(supplier.Code, code) < 0)));
        return Task.FromResult(count);
    }

    public Task<SaveOutcome> TrySaveAsync(CancellationToken cancellationToken)
    {
        if (NextOutcome is { } outcome)
        {
            NextOutcome = null;
            return Task.FromResult(outcome);
        }

        if (!RejectNextSave)
        {
            return Task.FromResult(SaveOutcome.Saved);
        }

        RejectNextSave = false;
        return Task.FromResult(SaveOutcome.Duplicate);
    }
}

internal sealed class FakeSupplierPurchaseStatsReader : ISupplierPurchaseStatsReader
{
    public Dictionary<Guid, SupplierPurchaseStats> Stats { get; } = [];

    public Task<IReadOnlyDictionary<Guid, SupplierPurchaseStats>> ReadAsync(
        IReadOnlyCollection<Guid> supplierIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, SupplierPurchaseStats>>(Stats);
}
