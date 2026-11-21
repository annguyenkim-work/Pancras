using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Catalog;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Notifications;

using Pancras.Tests.Shared;

namespace Pancras.Web.Tests;

// Test double của Kho, dùng chung cho mọi test trang.

internal sealed class InventoryDirectoryDouble : IInventoryRepository
{
    public List<StockBalanceRow> Rows { get; } = [];

    public List<StockBalance> Balances { get; } = [];

    public List<StockLocation> Locations { get; } = [];

    public List<TransferSlip> Transfers { get; } = [];

    public List<StockMovement> Movements { get; } = [];

    public List<StockLocation> DeletedLocations { get; } = [];

    public Task<IReadOnlyList<StockLocation>> ListDeletedLocationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockLocation>>(DeletedLocations.ToArray());

    public Task<IReadOnlyList<StockLocation>> ListLocationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockLocation>>(Locations.ToArray());

    public Task<IReadOnlyList<StockItemChoice>> ListItemChoicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockItemChoice>>([]);

    public Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(ListStockBalancesQuery filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockBalanceRow>>(Rows.Where(filter.Matches).ToArray());

    public Task<int> SumOnHandAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult(Balances.Where(balance => balance.CatalogItemId == catalogItemId).Sum(balance => balance.OnHand));

    public Task<StockBalance?> FindBalanceForUpdateAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken) =>
        Task.FromResult(Balances.FirstOrDefault(balance => balance.CatalogItemId == catalogItemId && balance.StockLocationId == stockLocationId));

    public Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken)
    {
        Balances.Add(balance);
        return Task.CompletedTask;
    }

    public Task AddTransferAsync(TransferSlip slip, CancellationToken cancellationToken)
    {
        Transfers.Add(slip);
        return Task.CompletedTask;
    }

    public Task<Guid?> FindTransferIdByRequestKeyAsync(Guid requestKey, Guid requestedById, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(null);

    public Task<TransferSlip?> FindTransferForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Transfers.FirstOrDefault(slip => slip.Id == id));

    public Task<TransferSlip?> FindTransferReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        FindTransferForUpdateAsync(id, cancellationToken);

    public List<TransferEvent> TransferEvents { get; } = [];

    public Task AddTransferEventAsync(TransferEvent transferEvent, CancellationToken cancellationToken)
    {
        TransferEvents.Add(transferEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, int>> ListTransferNumbersAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(Transfers.Where(slip => transferSlipIds.Contains(slip.Id)).ToDictionary(slip => slip.Id, slip => slip.Number));

    public Task<IReadOnlyList<TransferEvent>> ListTransferEventsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferEvent>>(TransferEvents.Where(item => transferSlipIds.Contains(item.TransferSlipId)).ToArray());

    public Task<StockItemProfile?> FindItemProfileAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<StockItemProfile?>(null);

    public Task<IReadOnlyList<StockBalanceRow>> ListItemBalancesAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockBalanceRow>>(Rows.Where(row => row.CatalogItemId == catalogItemId).ToArray());

    public Task<IReadOnlyList<StockMovement>> ListItemMovementsAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockMovement>>([]);

    public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StockMovement>> ListHistoryAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockMovement>>([]);

    public List<StocktakeSheet> Stocktakes { get; } = [];

    public Task AddStocktakeAsync(StocktakeSheet sheet, CancellationToken cancellationToken)
    {
        Stocktakes.Add(sheet);
        return Task.CompletedTask;
    }

    public Task<StocktakeSheet?> FindStocktakeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Stocktakes.FirstOrDefault(sheet => sheet.Id == id));

    public List<QuantityAdjustment> Adjustments { get; } = [];

    public Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken)
    {
        Adjustments.Add(adjustment);
        return Task.CompletedTask;
    }

    public Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Adjustments.FirstOrDefault(item => item.Id == id));

    public List<DisposalSlip> Disposals { get; } = [];

    public Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken)
    {
        Disposals.Add(slip);
        return Task.CompletedTask;
    }

    public Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Disposals.FirstOrDefault(slip => slip.Id == id));

    public Task<Guid?> FindDocumentLocationAsync(StockDocumentKind kind, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(null);

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

    public Task<StockDocumentCounts> CountStockDocumentsAsync(Guid? scope, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(FakeStockDocuments.Count(Stocktakes, Adjustments, Disposals, scope, filter));

    public Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StocktakeSheet>>(FakeStockDocuments.Stocktakes(Stocktakes, scope, filter).Skip(range.Skip).Take(range.Take).ToArray());

    public Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuantityAdjustment>>(FakeStockDocuments.Adjustments(Adjustments, scope, filter).Skip(range.Skip).Take(range.Take).ToArray());

    public Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DisposalSlip>>(FakeStockDocuments.Disposals(Disposals, scope, filter).Skip(range.Skip).Take(range.Take).ToArray());

    public Task<StockDocumentPosition?> LocateStockDocumentAsync(Guid? scope, Guid id, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult(FakeStockDocuments.Locate(Stocktakes, Adjustments, Disposals, scope, filter, id));

    public Task<(IReadOnlyList<StocktakeSheet> Stocktakes, IReadOnlyList<QuantityAdjustment> Adjustments, IReadOnlyList<DisposalSlip> Disposals)> FindStockDocumentsAsync(
        Guid? scope,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<StocktakeSheet>, IReadOnlyList<QuantityAdjustment>, IReadOnlyList<DisposalSlip>)>((
            FakeStockDocuments.Stocktakes(Stocktakes, scope).Where(sheet => ids.Contains(sheet.Id)).ToArray(),
            FakeStockDocuments.Adjustments(Adjustments, scope).Where(item => ids.Contains(item.Id)).ToArray(),
            FakeStockDocuments.Disposals(Disposals, scope).Where(slip => ids.Contains(slip.Id)).ToArray()));

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



    private IEnumerable<string> ItemText(Guid itemId) =>
        Rows.Where(row => row.CatalogItemId == itemId).SelectMany(row => new[] { row.Code, row.Name });

    public Task AddNoticeAsync(Notification notice, CancellationToken cancellationToken) => Task.CompletedTask;

    public List<Notification> Notices { get; } = [];

    public Task<NoticePage> ListNoticesPageAsync(Guid recipientEmployeeId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var mine = Notices.Where(notice => notice.RecipientEmployeeId == recipientEmployeeId)
            .OrderByDescending(notice => notice.CreatedAt).ToArray();
        return Task.FromResult(new NoticePage(mine.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), mine.Length));
    }

    public Task<int> CountUnseenNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult(Notices.Count(notice => notice.RecipientEmployeeId == recipientEmployeeId && !notice.Seen));

    public Task<Notification?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<Notification?>(null);

    public Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken) => Task.CompletedTask;

}

internal sealed class TransferAttachmentDouble : ITransferAttachmentRepository
{
    public Task<IReadOnlyList<TransferAttachment>> ListAsync(Guid transferSlipId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferAttachment>>([]);

    public Task<TransferAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<TransferAttachment?>(null);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>> ListForSlipsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>>(new Dictionary<Guid, IReadOnlyList<TransferAttachment>>());

    public Task AddAsync(TransferAttachment attachment, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class StockDocumentAttachmentDouble : IStockDocumentAttachmentRepository
{
    public Task<IReadOnlyList<StockDocumentAttachment>> ListAsync(StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockDocumentAttachment>>([]);

    public Task<StockDocumentAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<StockDocumentAttachment?>(null);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>>(new Dictionary<Guid, IReadOnlyList<StockDocumentAttachment>>());

    public Task AddAsync(StockDocumentAttachment attachment, CancellationToken cancellationToken) => Task.CompletedTask;
}
