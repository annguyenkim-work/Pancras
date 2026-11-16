using Microsoft.Extensions.Logging;

namespace Pancras.Application.Catalog;

public sealed class ListCatalogItemsHandler(
    ICatalogItemRepository repository,
    ILogger<ListCatalogItemsHandler> logger)
{
    public async Task<CatalogItemPage> HandleAsync(
        ListCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.CategoryRejected)
        {
            logger.LogInformation("Listed {CatalogItemCount} catalog items.", 0);
            return new CatalogItemPage([], 1, query.PageSize, 0);
        }

        var slice = await repository.ListAsync(query, query.PageNumber, query.PageSize, cancellationToken);
        logger.LogInformation("Listed {CatalogItemCount} catalog items.", slice.Items.Count);
        var rows = slice.Items.Select(CatalogItemRow.From).ToArray();
        return new CatalogItemPage(rows, slice.PageNumber, query.PageSize, slice.TotalCount);
    }
}
