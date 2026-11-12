using Microsoft.Extensions.Logging;

namespace Pancras.Application.Catalog;

public sealed class ListMatchingCatalogItemsHandler(
    ICatalogItemRepository repository,
    ILogger<ListMatchingCatalogItemsHandler> logger)
{
    public async Task<IReadOnlyList<CatalogItemRow>> HandleAsync(
        CatalogListFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.CategoryRejected)
        {
            logger.LogInformation("Matched {CatalogItemCount} catalog items.", 0);
            return [];
        }

        var items = await repository.ListMatchingAsync(filter, cancellationToken);
        logger.LogInformation("Matched {CatalogItemCount} catalog items.", items.Count);
        return items.Select(CatalogItemRow.From).ToArray();
    }
}
