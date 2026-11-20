using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Notifications;

namespace Pancras.Application.Tests;

internal sealed class FakeInventoryStore : IInventoryRepository, Pancras.Application.Common.IUnitOfWork
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

    public Task<TransferSlip?> FindTransferReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        FindTransferForUpdateAsync(id, cancellationToken);

    public Task<Guid?> FindTransferIdByRequestKeyAsync(Guid requestKey, Guid requestedById, CancellationToken cancellationToken) =>
        Task.FromResult(Transfers.Concat(_stagedTransfers).Where(slip => slip.RequestKey == requestKey && slip.RequestedById == requestedById).Select(slip => (Guid?)slip.Id).FirstOrDefault());

    public List<TransferEvent> TransferEvents { get; } = [];

    public Task AddTransferEventAsync(TransferEvent transferEvent, CancellationToken cancellationToken)
    {
        TransferEvents.Add(transferEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, int>> ListTransferNumbersAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(Transfers.Where(slip => transferSlipIds.Contains(slip.Id)).ToDictionary(slip => slip.Id, slip => slip.Number));

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

    public Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken)
    {
        _stagedAdjustments.Add(adjustment);
        return Task.CompletedTask;
    }

    public Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Adjustments.FirstOrDefault(adjustment => adjustment.Id == id));

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

    // Bản trong bộ nhớ của các truy vấn trang (cùng ngữ nghĩa với InventoryRepository: lọc, sắp xếp, cắt trang).
    public Task<TransferPage> ListTransfersPageAsync(TransferListQuery query, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var visible = TransferSource().Where(query.Visible.Compile()).ToList();
        IEnumerable<TransferSlip> rows = visible;
        if (query.Waiting is { } waiting)
        {
            rows = rows.Where(waiting.Compile());
        }

        if (query.Status is TransferStatus status)
        {
            rows = rows.Where(slip => slip.Status == status);
        }

        if (query.LocationId is Guid place)
        {
            rows = rows.Where(slip => slip.SourceLocationId == place || slip.DestinationLocationId == place);
        }

        if (!string.IsNullOrWhiteSpace(query.Term))
        {
            var term = query.Term.Trim();
            rows = rows.Where(slip => StockDocumentCode.ForTransfer(slip.Number).Contains(term, StringComparison.OrdinalIgnoreCase)
                || slip.Lines.Any(line => ItemText(line.CatalogItemId).Any(text => text.Contains(term, StringComparison.OrdinalIgnoreCase))));
        }

        if (query.CreatedFrom is DateTimeOffset from)
        {
            rows = rows.Where(slip => CreatedAt(slip.Id) >= from);
        }

        if (query.CreatedBefore is DateTimeOffset before)
        {
            rows = rows.Where(slip => CreatedAt(slip.Id) < before);
        }

        var matching = rows
            .OrderBy(slip => slip.Status is TransferStatus.Requested or TransferStatus.Approved or TransferStatus.InTransit or TransferStatus.ReceivedWithDiscrepancy ? 0 : 1)
            .ThenByDescending(slip => slip.Id)
            .ToList();
        var size = Math.Max(1, pageSize);
        var page = Math.Clamp(pageNumber, 1, Math.Max(1, (matching.Count + size - 1) / size));
        return Task.FromResult(new TransferPage(matching.Skip((page - 1) * size).Take(size).ToArray(), matching.Count, page, visible.Count > 0));
    }

    public Task<int> CountTransfersAsync(System.Linq.Expressions.Expression<Func<TransferSlip, bool>> predicate, CancellationToken cancellationToken) =>
        Task.FromResult(TransferSource().Count(predicate.Compile()));

    public Task<IReadOnlyList<TransferSlip>> FindTransfersAsync(System.Linq.Expressions.Expression<Func<TransferSlip, bool>> visible, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferSlip>>(TransferSource().Where(visible.Compile()).Where(slip => ids.Contains(slip.Id)).ToArray());

    /// <summary>Áp bộ lọc loại và mã như database: mã là 8 ký tự hex cuối của Id, khớp không phân biệt hoa thường.</summary>
    private static IEnumerable<T> Matching<T>(IEnumerable<T> source, StockDocumentFilter filter, StockDocumentKind kind, string prefix)
        where T : class
    {
        if (!filter.Includes(kind))
        {
            return [];
        }

        return filter.CodeFragment is { } fragment
            ? source.Where(document => StockDocumentCode.Of(prefix, (Guid)document.GetType().GetProperty("Id")!.GetValue(document)!)[3..].Contains(fragment, StringComparison.OrdinalIgnoreCase))
            : source;
    }

    public Task<StockDocumentCounts> CountStockDocumentsAsync(Guid? scope, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(new StockDocumentCounts(
            Matching(ScopedStocktakes(scope), filter, StockDocumentKind.Stocktake, "KK").Count(), Matching(ScopedAdjustments(scope), filter, StockDocumentKind.Adjustment, "DC").Count(), Matching(ScopedDisposals(scope), filter, StockDocumentKind.Disposal, "HY").Count()));

    public Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StocktakeSheet>>(Matching(ScopedStocktakes(scope), filter, StockDocumentKind.Stocktake, "KK").Skip(range.Skip).Take(range.Take).ToArray());

    public Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuantityAdjustment>>(Matching(ScopedAdjustments(scope), filter, StockDocumentKind.Adjustment, "DC").Skip(range.Skip).Take(range.Take).ToArray());

    public Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DisposalSlip>>(Matching(ScopedDisposals(scope), filter, StockDocumentKind.Disposal, "HY").Skip(range.Skip).Take(range.Take).ToArray());

    public Task<StockDocumentPosition?> LocateStockDocumentAsync(Guid? scope, Guid id, StockDocumentFilter filter, CancellationToken cancellationToken)
    {
        var stocktake = Matching(ScopedStocktakes(scope), filter, StockDocumentKind.Stocktake, "KK").Select(sheet => sheet.Id).ToList().IndexOf(id);
        var adjustment = Matching(ScopedAdjustments(scope), filter, StockDocumentKind.Adjustment, "DC").Select(item => item.Id).ToList().IndexOf(id);
        var disposal = Matching(ScopedDisposals(scope), filter, StockDocumentKind.Disposal, "HY").Select(slip => slip.Id).ToList().IndexOf(id);
        return Task.FromResult(
            stocktake >= 0 ? new StockDocumentPosition(StockDocumentKind.Stocktake, stocktake)
            : adjustment >= 0 ? new StockDocumentPosition(StockDocumentKind.Adjustment, adjustment)
            : disposal >= 0 ? new StockDocumentPosition(StockDocumentKind.Disposal, disposal)
            : (StockDocumentPosition?)null);
    }

    public Task<(IReadOnlyList<StocktakeSheet> Stocktakes, IReadOnlyList<QuantityAdjustment> Adjustments, IReadOnlyList<DisposalSlip> Disposals)> FindStockDocumentsAsync(
        Guid? scope,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<StocktakeSheet>, IReadOnlyList<QuantityAdjustment>, IReadOnlyList<DisposalSlip>)>((
            ScopedStocktakes(scope).Where(sheet => ids.Contains(sheet.Id)).ToArray(),
            ScopedAdjustments(scope).Where(item => ids.Contains(item.Id)).ToArray(),
            ScopedDisposals(scope).Where(slip => ids.Contains(slip.Id)).ToArray()));

    private IEnumerable<StocktakeSheet> ScopedStocktakes(Guid? scope) =>
        StocktakeSource().Where(sheet => scope is not Guid only || sheet.StockLocationId == only).OrderByDescending(sheet => sheet.Id);

    private IEnumerable<QuantityAdjustment> ScopedAdjustments(Guid? scope) =>
        AdjustmentSource().Where(item => scope is not Guid only || item.StockLocationId == only).OrderByDescending(item => item.Id);

    private IEnumerable<DisposalSlip> ScopedDisposals(Guid? scope) =>
        DisposalSource().Where(slip => scope is not Guid only || slip.StockLocationId == only).OrderByDescending(slip => slip.Id);

    private static DateTimeOffset CreatedAt(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);
        long milliseconds = 0;
        for (var i = 0; i < 6; i++)
        {
            milliseconds = (milliseconds << 8) | bytes[i];
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private IEnumerable<TransferSlip> TransferSource() => Transfers;

    private IEnumerable<StocktakeSheet> StocktakeSource() => Stocktakes;

    private IEnumerable<QuantityAdjustment> AdjustmentSource() => Adjustments;

    private IEnumerable<DisposalSlip> DisposalSource() => Disposals;

    private IEnumerable<string> ItemText(Guid itemId) =>
        Items.Where(item => item.Id == itemId).SelectMany(item => new[] { item.Code, item.Name });

    public Task<Pancras.Application.Common.IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken) =>
        Task.FromResult<Pancras.Application.Common.IUnitOfWorkScope>(new FakeUnitOfWorkScope(Commit));

    private Pancras.Application.Common.SaveOutcome Commit()
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
        return Pancras.Application.Common.SaveOutcome.Saved;
    }
}

internal sealed record StockItemInfo(Guid Id, string Code, string Name, string Brand, ProductCategory Category, decimal CostPrice = 0);
