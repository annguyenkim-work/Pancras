using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pancras.Application.Catalog;
using Pancras.Domain.Catalog;

namespace Pancras.Infrastructure.Persistence;

public sealed class CatalogItemRepository(PancrasDbContext context) : ICatalogItemRepository
{
    public async Task<CatalogSlice> ListAsync(
        CatalogListFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (filter.CategoryRejected)
        {
            return new CatalogSlice([], 0, 1);
        }

        var ordered = Filtered(filter).OrderBy(item => item.Name).ThenBy(item => item.Code);
        var total = await ordered.CountAsync(cancellationToken);
        var size = pageSize < 1 ? CatalogPageRequest.DefaultSize : pageSize;
        var totalPages = total == 0 ? 1 : (total + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        var items = await ordered.Skip((number - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new CatalogSlice(items, total, number);
    }

    public async Task<IReadOnlyList<CatalogItem>> ListMatchingAsync(
        CatalogListFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.CategoryRejected)
        {
            return [];
        }

        var items = await Filtered(filter)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return items;
    }

    public Task<CatalogItem?> GetVisibleReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.CatalogItems.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsCombo, cancellationToken);

    public Task<CatalogItem?> GetVisibleForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.CatalogItems.FirstOrDefaultAsync(item => item.Id == id && !item.IsCombo, cancellationToken);

    public Task<CatalogItem?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.CatalogItems.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.CatalogItems.AsNoTracking().AnyAsync(item => item.NormalizedCode == normalizedCode, cancellationToken);

    public async Task<bool> TryAddAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        await context.CatalogItems.AddAsync(item, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.Entry(item).State = EntityState.Detached;
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
        catch (DbUpdateException exception) when (IsUniqueViolation(exception) || exception is DbUpdateConcurrencyException)
        {
            foreach (var entry in context.ChangeTracker.Entries<CatalogItem>().ToList())
            {
                if (entry.State == EntityState.Modified)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }

            return false;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, CatalogItemMeasure>> ListMeasuresAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, CatalogItemMeasure>();
        }

        var rows = await context.CatalogItems.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .Select(item => new CatalogItemMeasure(item.Id, item.WeightKg, item.VolumeM3))
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id);
    }

    private IQueryable<CatalogItem> Filtered(CatalogListFilter filter)
    {
        var query = context.CatalogItems.AsNoTracking().Where(item => !item.IsCombo);
        if (filter.Category is ProductCategory category)
        {
            query = query.Where(item => item.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var pattern = CatalogTextSearch.ContainsPattern(filter.Term);
            query = query.Where(item =>
                EF.Functions.ILike(item.Code, pattern)
                || EF.Functions.ILike(item.Name, pattern)
                || EF.Functions.ILike(item.Brand, pattern));
        }

        return query;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
