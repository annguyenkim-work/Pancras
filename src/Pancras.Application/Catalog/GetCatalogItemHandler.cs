using Microsoft.Extensions.Logging;
using Pancras.Application.Common;

namespace Pancras.Application.Catalog;

public sealed class GetCatalogItemHandler(
    ICatalogItemRepository repository,
    ILogger<GetCatalogItemHandler> logger)
{
    public async Task<Result<CatalogItemRow>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetVisibleReadOnlyAsync(id, cancellationToken);
        if (item is null)
        {
            logger.LogInformation("Catalog item {CatalogItemId} was not visible.", id);
            return Result<CatalogItemRow>.Fail(CatalogError.NotFound);
        }

        return Result<CatalogItemRow>.Ok(CatalogItemRow.From(item));
    }
}
