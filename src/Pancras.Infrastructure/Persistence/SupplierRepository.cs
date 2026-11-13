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
            .Where(supplier => !supplier.IsDeleted)
            .OrderBy(supplier => supplier.Name)
            .ThenBy(supplier => supplier.Code)
            .ToListAsync(cancellationToken);
        return suppliers;
    }

    public async Task<SupplierSlice> ListFilteredAsync(
        string? term,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var suppliers = context.Suppliers.AsNoTracking().Where(supplier => !supplier.IsDeleted);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = SupplierTextSearch.ContainsPattern(term);
            suppliers = suppliers.Where(supplier =>
                EF.Functions.ILike(supplier.Code, pattern) || EF.Functions.ILike(supplier.Name, pattern));
        }

        var ordered = suppliers
            .OrderBy(supplier => supplier.Name)
            .ThenBy(supplier => supplier.Code);
        var total = await ordered.CountAsync(cancellationToken);
        var size = pageSize < 1 ? SupplierPageRequest.DefaultSize : pageSize;
        var totalPages = total == 0 ? 1 : (total + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        var items = await ordered
            .Skip((number - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new SupplierSlice(items, total, number);
    }

    public Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking().CountAsync(
            supplier => !supplier.IsDeleted
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
            .FirstOrDefaultAsync(supplier => supplier.Id == id && !supplier.IsDeleted, cancellationToken);

    public Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Suppliers.FirstOrDefaultAsync(supplier => supplier.Id == id && !supplier.IsDeleted, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var active = context.Suppliers.AsNoTracking().Where(supplier => !supplier.IsDeleted && supplier.NormalizedCode == normalizedCode);
        if (excludingId is Guid id)
        {
            active = active.Where(supplier => supplier.Id != id);
        }

        return active.AnyAsync(cancellationToken);
    }

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

    public async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            foreach (var entry in context.ChangeTracker.Entries<Supplier>().ToList())
            {
                if (entry.State == EntityState.Modified)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }

            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
