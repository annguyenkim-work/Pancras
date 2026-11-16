using Pancras.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pancras.Application.Suppliers;
using Pancras.Domain.Suppliers;

namespace Pancras.Infrastructure.Persistence;

public sealed class SupplierRepository(PancrasDbContext context) : ISupplierRepository
{
    public async Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken)
    {
        var suppliers = await context.Suppliers.AsNoTracking()
            .Where(supplier => supplier.IsActive)
            .OrderBy(supplier => supplier.Name)
            .ThenBy(supplier => supplier.Code)
            .ToListAsync(cancellationToken);
        return suppliers;
    }

    public async Task<SupplierSlice> ListFilteredAsync(
        ListSuppliersQuery filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Supplier> suppliers = context.Suppliers.AsNoTracking();
        suppliers = filter.Status switch
        {
            SupplierStatusFilter.Inactive => suppliers.Where(supplier => !supplier.IsActive),
            SupplierStatusFilter.All => suppliers,
            _ => suppliers.Where(supplier => supplier.IsActive)
        };

        if (filter.Country is not null)
        {
            var country = filter.Country;
            suppliers = suppliers.Where(supplier => supplier.Country == country);
        }

        if (filter.Term is not null)
        {
            var pattern = SupplierTextSearch.ContainsPattern(filter.Term);
            suppliers = suppliers.Where(supplier =>
                EF.Functions.ILike(supplier.Code, pattern)
                || EF.Functions.ILike(supplier.Name, pattern)
                || EF.Functions.ILike(supplier.ContactName, pattern)
                || EF.Functions.ILike(supplier.Email, pattern)
                || EF.Functions.ILike(supplier.Phone, pattern)
                || EF.Functions.ILike(supplier.Country, pattern));
        }

        var ordered = SupplierOrdering.Apply(suppliers, filter.Order);
        var total = await ordered.CountAsync(cancellationToken);
        var size = pageSize < 1 ? Pancras.Application.Common.PageSizes.Default : pageSize;
        var totalPages = total == 0 ? 1 : (total + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        var items = await ordered
            .Skip((number - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new SupplierSlice(items, total, number);
    }

    public async Task<IReadOnlyList<string>> ListCountriesAsync(CancellationToken cancellationToken) =>
        await context.Suppliers.AsNoTracking()
            .Select(supplier => supplier.Country)
            .Distinct()
            .OrderBy(country => country)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking().CountAsync(
            supplier => supplier.IsActive
                && (supplier.Name.CompareTo(name) < 0
                    || (supplier.Name == name && supplier.Code.CompareTo(code) < 0)),
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        return await context.Suppliers.AsNoTracking()
            .Where(supplier => wanted.Contains(supplier.Id))
            .ToDictionaryAsync(supplier => supplier.Id, supplier => supplier.Name, cancellationToken);
    }

    public Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(supplier => supplier.Id == id && supplier.IsActive, cancellationToken);

    public Task<Supplier?> GetAnyReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking().FirstOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);

    public Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Suppliers.FirstOrDefaultAsync(supplier => supplier.Id == id && supplier.IsActive, cancellationToken);

    public Task<Supplier?> GetAnyForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Suppliers.FirstOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var active = context.Suppliers.AsNoTracking().Where(supplier => supplier.IsActive && supplier.NormalizedCode == normalizedCode);
        if (excludingId is Guid id)
        {
            active = active.Where(supplier => supplier.Id != id);
        }

        return active.AnyAsync(cancellationToken);
    }

    public Task<string?> InactiveNameWithCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking()
            .Where(supplier => !supplier.IsActive && supplier.NormalizedCode == normalizedCode)
            .Select(supplier => (string?)supplier.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryAddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        await context.Suppliers.AddAsync(supplier, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.Entry(supplier).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<SaveOutcome> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception) || exception is DbUpdateConcurrencyException)
        {
            foreach (var entry in context.ChangeTracker.Entries<Supplier>().ToList())
            {
                if (entry.State == EntityState.Modified)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }

            return exception is DbUpdateConcurrencyException ? SaveOutcome.Conflict : SaveOutcome.Duplicate;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
