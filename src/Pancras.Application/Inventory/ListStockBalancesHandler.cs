using Microsoft.Extensions.Logging;

namespace Pancras.Application.Inventory;

public sealed class ListStockBalancesHandler(
    IInventoryRepository repository,
    ILogger<ListStockBalancesHandler> logger)
{
    public async Task<IReadOnlyList<StockBalanceRow>> HandleAsync(StockListFilter filter, CancellationToken cancellationToken)
    {
        var rows = await repository.ListBalancesAsync(filter, cancellationToken);
        logger.LogInformation("Listed {StockBalanceCount} stock balances.", rows.Count);
        return rows;
    }

    public async Task<IReadOnlyList<StockBalanceRow>> ExportAsync(StockListFilter filter, CancellationToken cancellationToken)
    {
        var rows = await repository.ListBalancesAsync(filter, cancellationToken);
        logger.LogInformation("Exported {StockBalanceCount} stock balances.", rows.Count);
        return rows;
    }
}
