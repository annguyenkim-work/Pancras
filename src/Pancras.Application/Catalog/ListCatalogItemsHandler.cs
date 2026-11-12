using Microsoft.Extensions.Logging;

namespace Pancras.Application.Catalog;

public sealed class ListCatalogItemsHandler(
    ICatalogItemRepository repository,
    ILogger<ListCatalogItemsHandler> logger)
{
    public async Task<CatalogItemPage> HandleAsync(
        CatalogListFilter filter,
        CatalogPageRequest page,
        CancellationToken cancellationToken)
    {
        if (filter.CategoryRejected)
        {
            logger.LogInformation("Listed {CatalogItemCount} catalog items.", 0);
            return new CatalogItemPage([], 1, page.Size, 0);
        }

        var slice = await repository.ListAsync(filter, page.Number, page.Size, cancellationToken);
        logger.LogInformation("Listed {CatalogItemCount} catalog items.", slice.Items.Count);
        var rows = slice.Items.Select(CatalogItemRow.From).ToArray();
        return new CatalogItemPage(rows, slice.PageNumber, page.Size, slice.TotalCount);
    }
}
