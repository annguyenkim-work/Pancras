using Microsoft.Extensions.Logging;

namespace Pancras.Application.Catalog;

public sealed class GetCatalogMeasuresHandler(
    ICatalogItemRepository repository,
    ILogger<GetCatalogMeasuresHandler> logger)
{
    public async Task<IReadOnlyDictionary<Guid, CatalogItemMeasure>> HandleAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var measures = await repository.ListMeasuresAsync(ids, cancellationToken);
        logger.LogInformation("Read measures for {CatalogItemCount} catalog items.", ids.Count);
        return measures;
    }
}
