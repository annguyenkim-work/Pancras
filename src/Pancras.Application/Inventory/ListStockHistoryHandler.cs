using Microsoft.Extensions.Logging;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ListStockHistoryHandler(
    IInventoryRepository repository,
    ILogger<ListStockHistoryHandler> logger)
{
    public async Task<IReadOnlyList<StockHistoryRow>> HandleAsync(
        Guid catalogItemId,
        Guid stockLocationId,
        CancellationToken cancellationToken)
    {
        if (catalogItemId == Guid.Empty || stockLocationId == Guid.Empty)
        {
            logger.LogInformation("Listed {StockMovementCount} stock movements.", 0);
            return [];
        }

        var movements = await repository.ListHistoryAsync(catalogItemId, stockLocationId, cancellationToken);
        logger.LogInformation(
            "Listed {StockMovementCount} stock movements for {CatalogItemId}.",
            movements.Count,
            catalogItemId);
        return movements
            .Select(movement => new StockHistoryRow(movement.Kind, movement.Quantity, movement.OccurredAt, movement.DocumentId))
            .ToArray();
    }
}
