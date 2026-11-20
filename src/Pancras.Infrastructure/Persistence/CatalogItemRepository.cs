using Pancras.Application.Common;
using Microsoft.EntityFrameworkCore;
using Pancras.Application.Catalog;
using Pancras.Domain.Catalog;

namespace Pancras.Infrastructure.Persistence;

public sealed class CatalogItemRepository(PancrasDbContext context) : ICatalogItemRepository
{
    public async Task<CatalogSlice> ListAsync(
        ListCatalogItemsQuery filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (filter.CategoryRejected)
        {
            return new CatalogSlice([], 0, 1);
        }

        var ordered = CatalogOrdering.Apply(Filtered(filter), filter.Order);
        var total = await ordered.CountAsync(cancellationToken);
        var size = pageSize < 1 ? Pancras.Application.Common.PageSizes.Default : pageSize;
        var totalPages = total == 0 ? 1 : (total + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        var items = await ordered.Skip((number - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new CatalogSlice(items, total, number);
    }

    public async Task<IReadOnlyList<CatalogItem>> ListMatchingAsync(
        ListCatalogItemsQuery filter,
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

    public Task AddAsync(CatalogItem item, CancellationToken cancellationToken) =>
        context.CatalogItems.AddAsync(item, cancellationToken).AsTask();

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

    private IQueryable<CatalogItem> Filtered(ListCatalogItemsQuery filter)
    {
        var query = context.CatalogItems.AsNoTracking().Where(item => !item.IsCombo);
        query = filter.Status switch
        {
            CatalogStatusFilter.Disabled => query.Where(item => !item.IsActive),
            CatalogStatusFilter.All => query,
            _ => query.Where(item => item.IsActive)
        };
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
}
