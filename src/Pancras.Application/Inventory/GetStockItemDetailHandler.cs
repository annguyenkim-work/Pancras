using Microsoft.Extensions.Logging;

namespace Pancras.Application.Inventory;

public sealed class GetStockItemDetailHandler(
    IInventoryRepository repository,
    ILogger<GetStockItemDetailHandler> logger)
{
    public async Task<StockItemDetail?> HandleAsync(Guid catalogItemId, CancellationToken cancellationToken)
    {
        if (catalogItemId == Guid.Empty)
        {
            return null;
        }

        var profile = await repository.FindItemProfileAsync(catalogItemId, cancellationToken);
        if (profile is null)
        {
            logger.LogInformation("Stock detail missed {CatalogItemId}.", catalogItemId);
            return null;
        }

        var balances = await repository.ListItemBalancesAsync(catalogItemId, cancellationToken);
        var movements = await repository.ListItemMovementsAsync(catalogItemId, cancellationToken);
        var locations = await repository.ListLocationsAsync(cancellationToken);
        var names = locations.ToDictionary(location => location.Id, location => location.Name);
        var history = movements
            .Select(movement => new StockHistoryEntry(
                movement.Kind,
                movement.Quantity,
                movement.OccurredAt,
                movement.DocumentId,
                movement.StockLocationId,
                names.TryGetValue(movement.StockLocationId, out var name) ? name : string.Empty))
            .ToArray();
        logger.LogInformation(
            "Read stock detail for {CatalogItemId} with {StockMovementCount} movements.",
            catalogItemId,
            history.Length);
        return new StockItemDetail(profile, balances, history);
    }
}
