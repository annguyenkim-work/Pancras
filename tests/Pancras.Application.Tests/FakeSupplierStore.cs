using Pancras.Application.Suppliers;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

internal sealed class FakeSupplierRepository : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public bool RejectNextSave { get; set; }

    public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Supplier>>(ActiveOrdered(null));

    public Task<SupplierSlice> ListFilteredAsync(
        string? term,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var ordered = ActiveOrdered(term);
        var size = pageSize < 1 ? SupplierPageRequest.DefaultSize : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<Supplier> items = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new SupplierSlice(items, ordered.Count, number));
    }

    private List<Supplier> ActiveOrdered(string? term)
    {
        IEnumerable<Supplier> suppliers = Suppliers.Where(supplier => !supplier.IsDeleted);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var needle = term.Trim();
            suppliers = suppliers.Where(supplier =>
                supplier.Code.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        return suppliers.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Code).ToList();
    }

    public Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id && !supplier.IsDeleted));

    public Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetActiveReadOnlyAsync(id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Any(supplier =>
            !supplier.IsDeleted
            && supplier.NormalizedCode == normalizedCode
            && supplier.Id != excludingId));

    public Task<bool> TryAddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        if (Suppliers.Any(existing => !existing.IsDeleted && existing.NormalizedCode == supplier.NormalizedCode))
        {
            return Task.FromResult(false);
        }

        Suppliers.Add(supplier);
        return Task.FromResult(true);
    }

    public Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken)
    {
        var count = Suppliers.Count(supplier =>
            !supplier.IsDeleted
            && (string.CompareOrdinal(supplier.Name, name) < 0
                || (supplier.Name == name && string.CompareOrdinal(supplier.Code, code) < 0)));
        return Task.FromResult(count);
    }

    public Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        if (!RejectNextSave)
        {
            return Task.FromResult(true);
        }

        RejectNextSave = false;
        return Task.FromResult(false);
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
