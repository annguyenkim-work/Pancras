using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class GetStockItemDetailHandler(
    IInventoryRepository repository,
    ILogger<GetStockItemDetailHandler> logger)
{
    public async Task<StockItemDetail?> HandleAsync(Guid catalogItemId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (catalogItemId == Guid.Empty || !InventoryAccess.CanView(actor.Role))
        {
            return null;
        }

        var profile = await repository.FindItemProfileAsync(catalogItemId, cancellationToken);
        if (profile is null)
        {
            logger.LogInformation("Stock detail missed {CatalogItemId}.", catalogItemId);
            return null;
        }
        // Mọi vai trò xem kho đều thấy tồn và lịch sử ở mọi kho (giá vốn vẫn ẩn).
        var balances = (await repository.ListItemBalancesAsync(catalogItemId, cancellationToken)).ToArray();
        var movements = (await repository.ListItemMovementsAsync(catalogItemId, cancellationToken)).ToArray();
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
        if (!InventoryAccess.CanSeeCost(actor.Role))
        {
            profile = profile with { CostPrice = 0 };
            balances = balances.Select(balance => balance with { CostPrice = 0 }).ToArray();
        }

        return new StockItemDetail(profile, balances, history);
    }
}
