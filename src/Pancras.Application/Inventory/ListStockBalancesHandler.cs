using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ListStockBalancesHandler(
    IInventoryRepository repository,
    ILogger<ListStockBalancesHandler> logger)
{
    public async Task<IReadOnlyList<StockBalanceRow>> HandleAsync(
        StockListFilter filter,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        var rows = await LoadAsync(filter, actor, cancellationToken);
        logger.LogInformation("Listed {StockBalanceCount} stock balances.", rows.Count);
        return rows;
    }

    public async Task<IReadOnlyList<StockBalanceRow>> ExportAsync(
        StockListFilter filter,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanExport(actor.Role))
        {
            logger.LogWarning("Stock export forbidden for employee {EmployeeId}.", actor.Id);
            return [];
        }

        var rows = await LoadAsync(filter, actor, cancellationToken);
        logger.LogInformation("Exported {StockBalanceCount} stock balances.", rows.Count);
        return rows;
    }

    private async Task<IReadOnlyList<StockBalanceRow>> LoadAsync(
        StockListFilter filter,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanView(actor.Role))
        {
            return [];
        }

        var scoped = filter;
        if (InventoryAccess.Scope(actor) is Guid scope)
        {
            if (scope == Guid.Empty || (filter.LocationId is Guid requested && requested != scope))
            {
                return [];
            }

            scoped = filter with { LocationId = scope, LocationRejected = false };
        }

        var rows = await repository.ListBalancesAsync(scoped, cancellationToken);
        return InventoryAccess.CanSeeCost(actor.Role)
            ? rows
            : rows.Select(row => row with { CostPrice = 0 }).ToArray();
    }
}
