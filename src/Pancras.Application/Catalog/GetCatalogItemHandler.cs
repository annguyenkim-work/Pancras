using Microsoft.Extensions.Logging;
using Pancras.Application.Common;

namespace Pancras.Application.Catalog;

public sealed class GetCatalogItemHandler(
    ICatalogItemRepository repository,
    ILogger<GetCatalogItemHandler> logger)
{
    public async Task<Result<CatalogItemRow, CatalogError>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetVisibleReadOnlyAsync(id, cancellationToken);
        if (item is null)
        {
            logger.LogInformation("Catalog item {CatalogItemId} was not visible.", id);
            return Result<CatalogItemRow, CatalogError>.Fail(CatalogError.NotFound);
        }

        return Result<CatalogItemRow, CatalogError>.Ok(CatalogItemRow.From(item));
    }
}
