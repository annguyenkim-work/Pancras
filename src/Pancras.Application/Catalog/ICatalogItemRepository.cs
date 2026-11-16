using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

public interface ICatalogItemRepository
{
    Task<CatalogSlice> ListAsync(
        CatalogListFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogItem>> ListMatchingAsync(CatalogListFilter filter, CancellationToken cancellationToken);

    Task<CatalogItem?> GetVisibleReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<CatalogItem?> GetVisibleForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<CatalogItem?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(CatalogItem item, CancellationToken cancellationToken);

    Task<SaveOutcome> TrySaveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, CatalogItemMeasure>> ListMeasuresAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}
