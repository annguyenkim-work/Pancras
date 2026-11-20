using Pancras.Application.Common;
using Pancras.Application.Catalog;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Tests;

internal sealed class FakeCatalogRepository : ICatalogItemRepository, IUnitOfWork
{
    private readonly List<CatalogItem> _pending = [];

    public List<CatalogItem> Items { get; } = [];

    public int SaveCount { get; private set; }

    public Task<CatalogSlice> ListAsync(
        ListCatalogItemsQuery filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var ordered = Matching(filter);
        var size = pageSize < 1 ? Pancras.Application.Common.PageSizes.Default : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<CatalogItem> page = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new CatalogSlice(page, ordered.Count, number));
    }

    public Task<IReadOnlyList<CatalogItem>> ListMatchingAsync(
        ListCatalogItemsQuery filter,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogItem>>(Matching(filter));

    public Task<CatalogItem?> GetVisibleReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id && !item.IsCombo));

    public Task<CatalogItem?> GetVisibleForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetVisibleReadOnlyAsync(id, cancellationToken);

    public Task<CatalogItem?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(item => item.NormalizedCode == normalizedCode));

    public Task AddAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        _pending.Add(item);
        return Task.CompletedTask;
    }

    public SaveOutcome NextOutcome { get; set; } = SaveOutcome.Saved;

    public Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IUnitOfWorkScope>(new FakeUnitOfWorkScope(Commit));

    private SaveOutcome Commit()
    {
        SaveCount++;
        var staged = _pending.ToArray();
        _pending.Clear();
        if (NextOutcome != SaveOutcome.Saved)
        {
            return NextOutcome;
        }

        if (staged.Any(item => Items.Any(existing => existing.NormalizedCode == item.NormalizedCode)))
        {
            return SaveOutcome.Duplicate;
        }

        Items.AddRange(staged);
        return SaveOutcome.Saved;
    }

    public Task<IReadOnlyDictionary<Guid, CatalogItemMeasure>> ListMeasuresAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var rows = Items
            .Where(item => ids.Contains(item.Id))
            .Select(item => new CatalogItemMeasure(item.Id, item.WeightKg, item.VolumeM3))
            .ToDictionary(row => row.Id);
        return Task.FromResult<IReadOnlyDictionary<Guid, CatalogItemMeasure>>(rows);
    }

    private List<CatalogItem> Matching(ListCatalogItemsQuery filter) =>
        Items.Where(filter.Matches).OrderBy(item => item.Name).ThenBy(item => item.Code).ToList();
}
