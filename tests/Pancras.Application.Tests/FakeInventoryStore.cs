using Pancras.Domain.Notifications;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

internal sealed class FakeInventoryStore : IInventoryRepository
{
    private readonly List<StockBalance> _stagedBalances = [];
    private readonly List<TransferSlip> _stagedTransfers = [];
    private readonly List<StockMovement> _stagedMovements = [];
    private readonly List<StocktakeSheet> _stagedStocktakes = [];
    private readonly List<QuantityAdjustment> _stagedAdjustments = [];
    private readonly List<DisposalSlip> _stagedDisposals = [];
    private readonly List<Notification> _stagedNotices = [];

    public List<StockLocation> Locations { get; } = [];

    public List<StockItemInfo> Items { get; } = [];

    public List<StockBalance> Balances { get; } = [];

    public List<TransferSlip> Transfers { get; } = [];

    public List<StockMovement> Movements { get; } = [];

    public List<StocktakeSheet> Stocktakes { get; } = [];

    public List<QuantityAdjustment> Adjustments { get; } = [];

    public List<DisposalSlip> Disposals { get; } = [];

    public List<Notification> Notices { get; } = [];

    public List<StockLocation> DeletedLocations { get; } = [];

    public Task<IReadOnlyList<StockLocation>> ListDeletedLocationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockLocation>>(DeletedLocations.ToArray());

    public Task<IReadOnlyList<StockLocation>> ListLocationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockLocation>>(Locations.ToArray());

    public Task<IReadOnlyList<StockItemChoice>> ListItemChoicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockItemChoice>>(Items.Select(item => new StockItemChoice(item.Id, item.Code, item.Name)).ToArray());

    public Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(ListStockBalancesQuery filter, CancellationToken cancellationToken)
    {
        var rows = new List<StockBalanceRow>();
        foreach (var balance in Balances)
        {
            var item = Items.FirstOrDefault(candidate => candidate.Id == balance.CatalogItemId);
            var location = Locations.FirstOrDefault(candidate => candidate.Id == balance.StockLocationId);
            if (item is null || location is null)
            {
                continue;
            }

            var row = new StockBalanceRow(
                balance.CatalogItemId,
                item.Code,
                item.Name,
                item.Brand,
                item.Category,
                balance.StockLocationId,
                location.Name,
                balance.OnHand,
                balance.Reserved,
                balance.Available,
                balance.InTransit,
                CostPrice: item.CostPrice);
            if (filter.Matches(row))
            {
                rows.Add(row);
            }
        }

        IReadOnlyList<StockBalanceRow> result = rows;
        return Task.FromResult(result);
    }

    public Task<StockBalance?> FindBalanceForUpdateAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken) =>
        Task.FromResult(Balances.FirstOrDefault(balance => balance.CatalogItemId == catalogItemId && balance.StockLocationId == stockLocationId));

    public Task<int> SumOnHandAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult(Balances.Where(balance => balance.CatalogItemId == catalogItemId).Sum(balance => balance.OnHand));

    public Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken)
    {
        _stagedBalances.Add(balance);
        return Task.CompletedTask;
    }

    public Task AddTransferAsync(TransferSlip slip, CancellationToken cancellationToken)
    {
        _stagedTransfers.Add(slip);
        return Task.CompletedTask;
    }

    public Task<TransferSlip?> FindTransferForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Transfers.FirstOrDefault(slip => slip.Id == id));

    public Task<Guid?> FindTransferIdByRequestKeyAsync(Guid requestKey, Guid requestedById, CancellationToken cancellationToken) =>
        Task.FromResult(Transfers.Concat(_stagedTransfers).Where(slip => slip.RequestKey == requestKey && slip.RequestedById == requestedById).Select(slip => (Guid?)slip.Id).FirstOrDefault());

    public Task<IReadOnlyList<TransferSlip>> ListOpenTransfersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferSlip>>(Transfers.Where(slip => slip.Status is TransferStatus.Draft or TransferStatus.InTransit).ToArray());

    public Task<IReadOnlyList<TransferSlip>> ListTransfersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferSlip>>(Transfers.ToArray());

    public List<TransferEvent> TransferEvents { get; } = [];

    public Task AddTransferEventAsync(TransferEvent transferEvent, CancellationToken cancellationToken)
    {
        TransferEvents.Add(transferEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TransferEvent>> ListTransferEventsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferEvent>>(TransferEvents.Where(item => transferSlipIds.Contains(item.TransferSlipId)).OrderBy(item => item.OccurredAt).ToArray());

    public Task<StockItemProfile?> FindItemProfileAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<StockItemProfile?>(null);

    public Task<IReadOnlyList<StockBalanceRow>> ListItemBalancesAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        ListBalancesAsync(new ListStockBalancesQuery(null, false, null, false, null), cancellationToken)
            .ContinueWith(
                task => (IReadOnlyList<StockBalanceRow>)task.Result.Where(row => row.CatalogItemId == catalogItemId).ToArray(),
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

    public Task<IReadOnlyList<StockMovement>> ListItemMovementsAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(Movements
            .Where(movement => movement.CatalogItemId == catalogItemId)
            .OrderByDescending(movement => movement.OccurredAt)
            .ToArray());

    public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        _stagedMovements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StockMovement>> ListHistoryAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(Movements
            .Where(movement => movement.CatalogItemId == catalogItemId && movement.StockLocationId == stockLocationId)
            .OrderBy(movement => movement.OccurredAt)
            .ToArray());

    public Task AddStocktakeAsync(StocktakeSheet sheet, CancellationToken cancellationToken)
    {
        _stagedStocktakes.Add(sheet);
        return Task.CompletedTask;
    }

    public Task<StocktakeSheet?> FindStocktakeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Stocktakes.FirstOrDefault(sheet => sheet.Id == id));

    public Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StocktakeSheet>>(Stocktakes.ToArray());

    public Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken)
    {
        _stagedAdjustments.Add(adjustment);
        return Task.CompletedTask;
    }

    public Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Adjustments.FirstOrDefault(adjustment => adjustment.Id == id));

    public Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuantityAdjustment>>(Adjustments.ToArray());

    public Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken)
    {
        _stagedDisposals.Add(slip);
        return Task.CompletedTask;
    }

    public Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Disposals.FirstOrDefault(slip => slip.Id == id));

    public Task<Guid?> FindDocumentLocationAsync(StockDocumentKind kind, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(kind switch
        {
            StockDocumentKind.Stocktake => Stocktakes.FirstOrDefault(sheet => sheet.Id == id)?.StockLocationId,
            StockDocumentKind.Adjustment => Adjustments.FirstOrDefault(item => item.Id == id)?.StockLocationId,
            StockDocumentKind.Disposal => Disposals.FirstOrDefault(slip => slip.Id == id)?.StockLocationId,
            _ => null
        });

    public Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DisposalSlip>>(Disposals.ToArray());

    public Task AddNoticeAsync(Notification notice, CancellationToken cancellationToken)
    {
        _stagedNotices.Add(notice);
        return Task.CompletedTask;
    }

    public Task<NoticePage> ListNoticesPageAsync(Guid recipientEmployeeId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var mine = Notices.Where(notice => notice.RecipientEmployeeId == recipientEmployeeId)
            .OrderByDescending(notice => notice.CreatedAt).ToArray();
        return Task.FromResult(new NoticePage(mine.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), mine.Length));
    }

    public Task<int> CountUnseenNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult(Notices.Count(notice => notice.RecipientEmployeeId == recipientEmployeeId && !notice.Seen));

    public Task<Notification?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Notices.Concat(_stagedNotices).FirstOrDefault(notice => notice.Id == id));

    public Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken)
    {
        Notices.RemoveAll(notice => notice.DocumentId == documentId);
        _stagedNotices.RemoveAll(notice => notice.DocumentId == documentId);
        return Task.CompletedTask;
    }

    public Task<bool> CommitAsync(CancellationToken cancellationToken)
    {
        Balances.AddRange(_stagedBalances);
        Transfers.AddRange(_stagedTransfers);
        Movements.AddRange(_stagedMovements);
        Stocktakes.AddRange(_stagedStocktakes);
        Adjustments.AddRange(_stagedAdjustments);
        Disposals.AddRange(_stagedDisposals);
        Notices.AddRange(_stagedNotices);
        _stagedBalances.Clear();
        _stagedTransfers.Clear();
        _stagedMovements.Clear();
        _stagedStocktakes.Clear();
        _stagedAdjustments.Clear();
        _stagedDisposals.Clear();
        _stagedNotices.Clear();
        return Task.FromResult(true);
    }
}

internal sealed record StockItemInfo(Guid Id, string Code, string Name, string Brand, ProductCategory Category, decimal CostPrice = 0);
