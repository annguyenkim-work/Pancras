using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Catalog;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Web.Tests;

// Test double của Danh mục, dùng chung cho mọi test trang.

internal sealed class CatalogDirectoryDouble : ICatalogItemRepository
{
    public List<CatalogItem> Items { get; } = [];

    public Task<CatalogSlice> ListAsync(ListCatalogItemsQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var ordered = Matching(filter);
        var size = pageSize < 1 ? Pancras.Application.Common.PageSizes.Default : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<CatalogItem> page = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new CatalogSlice(page, ordered.Count, number));
    }

    public Task<IReadOnlyList<CatalogItem>> ListMatchingAsync(ListCatalogItemsQuery filter, CancellationToken cancellationToken) =>
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
        Items.Add(item);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, CatalogItemMeasure>> ListMeasuresAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, CatalogItemMeasure> rows = Items
            .Where(item => ids.Contains(item.Id))
            .ToDictionary(item => item.Id, item => new CatalogItemMeasure(item.Id, item.WeightKg, item.VolumeM3));
        return Task.FromResult(rows);
    }

    private List<CatalogItem> Matching(ListCatalogItemsQuery filter) =>
        Items.Where(filter.Matches).OrderBy(item => item.Name).ThenBy(item => item.Code).ToList();
}
