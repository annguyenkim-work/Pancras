using Pancras.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;

namespace Pancras.Infrastructure.Persistence;

public sealed class InventoryRepository(PancrasDbContext context, ILogger<InventoryRepository> logger) : IInventoryRepository
{
    public async Task<IReadOnlyList<StockLocation>> ListLocationsAsync(CancellationToken cancellationToken) =>
        await context.StockLocations.AsNoTracking().Where(location => location.IsActive).OrderBy(location => location.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockLocation>> ListDeletedLocationsAsync(CancellationToken cancellationToken) =>
        await context.StockLocations.AsNoTracking().Where(location => !location.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockItemChoice>> ListItemChoicesAsync(CancellationToken cancellationToken)
    {
        var items = await context.CatalogItems.AsNoTracking()
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Code)
            .Select(item => new StockItemChoice(item.Id, item.Code, item.Name, item.Unit, item.IsActive))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(ListStockBalancesQuery filter, CancellationToken cancellationToken)
    {
        if (filter.LocationRejected || filter.CategoryRejected)
        {
            return [];
        }

        var query =
            from balance in context.StockBalances.AsNoTracking()
            join item in context.CatalogItems.AsNoTracking() on balance.CatalogItemId equals item.Id
            join location in context.StockLocations.AsNoTracking().Where(place => place.IsActive) on balance.StockLocationId equals location.Id
            select new { balance, item, location };

        if (filter.LocationId is Guid locationId)
        {
            query = query.Where(row => row.balance.StockLocationId == locationId);
        }

        if (filter.Category is ProductCategory category)
        {
            query = query.Where(row => row.item.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var pattern = InventoryTextSearch.ContainsPattern(filter.Term);
            query = query.Where(row =>
                EF.Functions.ILike(row.item.Code, pattern)
                || EF.Functions.ILike(row.item.Name, pattern)
                || EF.Functions.ILike(row.item.Brand, pattern));
        }

        var rows = await query
            .OrderBy(row => row.item.Name)
            .ThenBy(row => row.location.Name)
            .Select(row => new StockBalanceRow(
                row.balance.CatalogItemId,
                row.item.Code,
                row.item.Name,
                row.item.Brand,
                row.item.Category,
                row.balance.StockLocationId,
                row.location.Name,
                row.balance.OnHand,
                row.balance.Reserved,
                row.balance.OnHand - row.balance.Reserved,
                row.balance.InTransit,
                row.item.Unit,
                row.item.Origin,
                row.item.CostPrice))
            .ToListAsync(cancellationToken);

        var stocked = context.StockBalances.Select(balance => balance.CatalogItemId);
        var missing = context.CatalogItems.AsNoTracking().Where(item => !item.IsCombo && item.IsActive && !stocked.Contains(item.Id));
        if (filter.Category is ProductCategory missingCategory)
        {
            missing = missing.Where(item => item.Category == missingCategory);
        }

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var pattern = InventoryTextSearch.ContainsPattern(filter.Term);
            missing = missing.Where(item =>
                EF.Functions.ILike(item.Code, pattern)
                || EF.Functions.ILike(item.Name, pattern)
                || EF.Functions.ILike(item.Brand, pattern));
        }

        var zeros = await missing
            .OrderBy(item => item.Name)
            .Select(item => new StockBalanceRow(
                item.Id,
                item.Code,
                item.Name,
                item.Brand,
                item.Category,
                Guid.Empty,
                string.Empty,
                0,
                0,
                0,
                0,
                item.Unit,
                item.Origin,
                item.CostPrice))
            .ToListAsync(cancellationToken);
        logger.LogInformation(
            "Listed {StockBalanceCount} stock rows and {UnreceivedItemCount} catalog items with no receipt.",
            rows.Count,
            zeros.Count);
        return rows
            .Concat(zeros)
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ThenBy(row => row.LocationName, StringComparer.Ordinal)
            .ToArray();
    }

    public Task<StockBalance?> FindBalanceForUpdateAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken) =>
        context.StockBalances.FirstOrDefaultAsync(
            balance => balance.CatalogItemId == catalogItemId && balance.StockLocationId == stockLocationId,
            cancellationToken);

    public async Task<int> SumOnHandAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        await context.StockBalances.Where(balance => balance.CatalogItemId == catalogItemId).SumAsync(balance => (int?)balance.OnHand, cancellationToken) ?? 0;

    public Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken) =>
        context.StockBalances.AddAsync(balance, cancellationToken).AsTask();

    public Task AddTransferAsync(TransferSlip slip, CancellationToken cancellationToken) =>
        context.TransferSlips.AddAsync(slip, cancellationToken).AsTask();

    /// <summary>
    /// Khóa dòng phiếu (SELECT ... FOR UPDATE) trong scope IUnitOfWork đang mở, rồi mới đọc phiếu.
    /// Hai thao tác đồng thời trên cùng một phiếu chạy lần lượt; thao tác đến sau đọc được trạng thái mới.
    /// Câu đọc phiếu chạy sau khi khóa được cấp để thấy dữ liệu đã commit của thao tác trước.
    /// </summary>
    public async Task<TransferSlip?> FindTransferForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireOpenScope();

        await context.Database
            .SqlQuery<int>($"SELECT 1 AS \"Value\" FROM transfer_slip WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return await context.TransferSlips.Include(slip => slip.Lines).FirstOrDefaultAsync(slip => slip.Id == id, cancellationToken);
    }

    public Task<TransferSlip?> FindTransferReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.TransferSlips.AsNoTracking().Include(slip => slip.Lines).FirstOrDefaultAsync(slip => slip.Id == id, cancellationToken);

    public async Task<Guid?> FindTransferIdByRequestKeyAsync(Guid requestKey, Guid requestedById, CancellationToken cancellationToken) =>
        await context.TransferSlips.AsNoTracking()
            .Where(slip => slip.RequestKey == requestKey && slip.RequestedById == requestedById)
            .Select(slip => (Guid?)slip.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TransferPage> ListTransfersPageAsync(TransferListQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var size = Math.Max(1, pageSize);
        var visible = context.TransferSlips.AsNoTracking().Where(filter.Visible);
        var anyVisible = await visible.AnyAsync(cancellationToken);
        var query = visible;
        if (filter.Waiting is { } waiting)
        {
            query = query.Where(waiting);
        }

        if (filter.Status is TransferStatus status)
        {
            query = query.Where(slip => slip.Status == status);
        }

        if (filter.LocationId is Guid place)
        {
            query = query.Where(slip => slip.SourceLocationId == place || slip.DestinationLocationId == place);
        }

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var pattern = InventoryTextSearch.ContainsPattern(filter.Term);
            var prefix = StockDocumentCode.Transfer + "-";
            // Mã phiếu CK-000123 dựng từ số thứ tự (cùng định dạng StockDocumentCode.ForTransfer), hoặc mã/tên một mặt hàng trên phiếu.
            query = query.Where(slip =>
                EF.Functions.ILike(prefix + slip.Number.ToString().PadLeft(6, '0'), pattern)
                || slip.Lines.Any(line => context.CatalogItems.Any(item =>
                    item.Id == line.CatalogItemId
                    && (EF.Functions.ILike(item.Code, pattern) || EF.Functions.ILike(item.Name, pattern)))));
        }

        if (filter.CreatedFrom is DateTimeOffset from)
        {
            var lower = Version7Floor(from);
            query = query.Where(slip => slip.Id.CompareTo(lower) >= 0);
        }

        if (filter.CreatedBefore is DateTimeOffset before)
        {
            var upper = Version7Floor(before);
            query = query.Where(slip => slip.Id.CompareTo(upper) < 0);
        }

        var total = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(pageNumber, 1, Math.Max(1, (total + size - 1) / size));
        var items = await query
            .OrderBy(slip => slip.Status == TransferStatus.Requested
                || slip.Status == TransferStatus.Approved
                || slip.Status == TransferStatus.InTransit
                || slip.Status == TransferStatus.ReceivedWithDiscrepancy ? 0 : 1)
            .ThenByDescending(slip => slip.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Include(slip => slip.Lines)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return new TransferPage(items, total, page, anyVisible);
    }

    public Task<int> CountTransfersAsync(System.Linq.Expressions.Expression<Func<TransferSlip, bool>> predicate, CancellationToken cancellationToken) =>
        context.TransferSlips.AsNoTracking().CountAsync(predicate, cancellationToken);

    public async Task<IReadOnlyList<TransferSlip>> FindTransfersAsync(
        System.Linq.Expressions.Expression<Func<TransferSlip, bool>> visible,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await context.TransferSlips.AsNoTracking()
                .Where(visible)
                .Where(slip => ids.Contains(slip.Id))
                .Include(slip => slip.Lines)
                .ToListAsync(cancellationToken);

    /// <summary>UUID v7 nhỏ nhất có mốc thời gian <paramref name="instant"/> (48 bit đầu là mili-giây Unix), để lọc theo lúc lập phiếu bằng Id.</summary>
    internal static Guid Version7Floor(DateTimeOffset instant)
    {
        var milliseconds = Math.Max(0, instant.ToUnixTimeMilliseconds());
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 0; i < 6; i++)
        {
            bytes[i] = (byte)(milliseconds >> (8 * (5 - i)));
        }

        return new Guid(bytes, bigEndian: true);
    }

    public Task AddTransferEventAsync(TransferEvent transferEvent, CancellationToken cancellationToken) =>
        context.TransferEvents.AddAsync(transferEvent, cancellationToken).AsTask();

    public async Task<IReadOnlyDictionary<Guid, int>> ListTransferNumbersAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        transferSlipIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await context.TransferSlips.AsNoTracking()
                .Where(slip => transferSlipIds.Contains(slip.Id))
                .ToDictionaryAsync(slip => slip.Id, slip => slip.Number, cancellationToken);

    public async Task<IReadOnlyList<TransferEvent>> ListTransferEventsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        transferSlipIds.Count == 0
            ? []
            : await context.TransferEvents.AsNoTracking()
                .Where(item => transferSlipIds.Contains(item.TransferSlipId))
                .OrderBy(item => item.OccurredAt)
                .ToListAsync(cancellationToken);

    public async Task<StockItemProfile?> FindItemProfileAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        await context.CatalogItems.AsNoTracking()
            .Where(item => item.Id == catalogItemId)
            .Select(item => new StockItemProfile(
                item.Id,
                item.Code,
                item.Name,
                item.Category,
                item.Brand,
                item.Origin,
                item.Unit,
                item.WeightKg,
                item.VolumeM3,
                item.WarrantyMonths,
                item.MinimumStock,
                item.CostPrice,
                item.RetailPrice,
                item.DealerPrice,
                item.DistributorPrice,
                item.ProjectPrice))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<StockBalanceRow>> ListItemBalancesAsync(Guid catalogItemId, CancellationToken cancellationToken)
    {
        var rows = await (
            from balance in context.StockBalances.AsNoTracking()
            join item in context.CatalogItems.AsNoTracking() on balance.CatalogItemId equals item.Id
            join location in context.StockLocations.AsNoTracking().Where(place => place.IsActive) on balance.StockLocationId equals location.Id
            where balance.CatalogItemId == catalogItemId
            orderby location.Name
            select new StockBalanceRow(
                balance.CatalogItemId,
                item.Code,
                item.Name,
                item.Brand,
                item.Category,
                balance.StockLocationId,
                location.Name,
                balance.OnHand,
                balance.Reserved,
                balance.OnHand - balance.Reserved,
                balance.InTransit,
                item.Unit,
                item.Origin,
                item.CostPrice))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<StockMovement>> ListItemMovementsAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        await context.StockMovements.AsNoTracking()
            .Where(movement => movement.CatalogItemId == catalogItemId)
            .OrderByDescending(movement => movement.OccurredAt)
            .ToListAsync(cancellationToken);

    public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken) =>
        context.StockMovements.AddAsync(movement, cancellationToken).AsTask();

    public async Task<IReadOnlyList<StockMovement>> ListHistoryAsync(
        Guid catalogItemId,
        Guid stockLocationId,
        CancellationToken cancellationToken) =>
        await context.StockMovements.AsNoTracking()
            .Where(movement => movement.CatalogItemId == catalogItemId && movement.StockLocationId == stockLocationId)
            .OrderBy(movement => movement.OccurredAt)
            .ToListAsync(cancellationToken);

    public Task AddStocktakeAsync(StocktakeSheet sheet, CancellationToken cancellationToken) =>
        context.StocktakeSheets.AddAsync(sheet, cancellationToken).AsTask();

    public Task<StocktakeSheet?> FindStocktakeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        LockedAsync("stocktake_sheet", id, context.StocktakeSheets, sheet => sheet.Id == id, cancellationToken);

    public Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken) =>
        context.QuantityAdjustments.AddAsync(adjustment, cancellationToken).AsTask();

    public Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        LockedAsync("quantity_adjustment", id, context.QuantityAdjustments, adjustment => adjustment.Id == id, cancellationToken);

    public Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken) =>
        context.DisposalSlips.AddAsync(slip, cancellationToken).AsTask();

    public Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        LockedAsync("disposal_slip", id, context.DisposalSlips, slip => slip.Id == id, cancellationToken);

    public async Task<StockDocumentCounts> CountStockDocumentsAsync(Guid? scope, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        new(
            await Matching(InScope(context.StocktakeSheets.AsNoTracking(), scope, sheet => sheet.StockLocationId), filter).CountAsync(cancellationToken),
            await Matching(InScope(context.QuantityAdjustments.AsNoTracking(), scope, item => item.StockLocationId), filter).CountAsync(cancellationToken),
            await Matching(InScope(context.DisposalSlips.AsNoTracking(), scope, slip => slip.StockLocationId), filter).CountAsync(cancellationToken));

    public async Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        range.Take <= 0
            ? []
            : await Matching(InScope(context.StocktakeSheets.AsNoTracking(), scope, sheet => sheet.StockLocationId), filter)
                .OrderByDescending(sheet => sheet.Id).Skip(range.Skip).Take(range.Take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        range.Take <= 0
            ? []
            : await Matching(InScope(context.QuantityAdjustments.AsNoTracking(), scope, item => item.StockLocationId), filter)
                .OrderByDescending(item => item.Id).Skip(range.Skip).Take(range.Take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken) =>
        range.Take <= 0
            ? []
            : await Matching(InScope(context.DisposalSlips.AsNoTracking(), scope, slip => slip.StockLocationId), filter)
                .OrderByDescending(slip => slip.Id).Skip(range.Skip).Take(range.Take).ToListAsync(cancellationToken);

    public async Task<StockDocumentPosition?> LocateStockDocumentAsync(Guid? scope, Guid id, StockDocumentFilter filter, CancellationToken cancellationToken)
    {
        // Thứ tự mới nhất trước: vị trí = số phiếu cùng loại (trong phạm vi) có Id lớn hơn.
        var stocktakes = Matching(InScope(context.StocktakeSheets.AsNoTracking(), scope, sheet => sheet.StockLocationId), filter);
        if (await stocktakes.AnyAsync(sheet => sheet.Id == id, cancellationToken))
        {
            return new StockDocumentPosition(StockDocumentKind.Stocktake, await stocktakes.CountAsync(sheet => sheet.Id.CompareTo(id) > 0, cancellationToken));
        }

        var adjustments = Matching(InScope(context.QuantityAdjustments.AsNoTracking(), scope, item => item.StockLocationId), filter);
        if (await adjustments.AnyAsync(item => item.Id == id, cancellationToken))
        {
            return new StockDocumentPosition(StockDocumentKind.Adjustment, await adjustments.CountAsync(item => item.Id.CompareTo(id) > 0, cancellationToken));
        }

        var disposals = Matching(InScope(context.DisposalSlips.AsNoTracking(), scope, slip => slip.StockLocationId), filter);
        if (await disposals.AnyAsync(slip => slip.Id == id, cancellationToken))
        {
            return new StockDocumentPosition(StockDocumentKind.Disposal, await disposals.CountAsync(slip => slip.Id.CompareTo(id) > 0, cancellationToken));
        }

        return null;
    }

    public async Task<(IReadOnlyList<StocktakeSheet> Stocktakes, IReadOnlyList<QuantityAdjustment> Adjustments, IReadOnlyList<DisposalSlip> Disposals)> FindStockDocumentsAsync(
        Guid? scope,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return ([], [], []);
        }

        var stocktakes = await InScope(context.StocktakeSheets.AsNoTracking(), scope, sheet => sheet.StockLocationId).Where(sheet => ids.Contains(sheet.Id)).ToListAsync(cancellationToken);
        var adjustments = await InScope(context.QuantityAdjustments.AsNoTracking(), scope, item => item.StockLocationId).Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        var disposals = await InScope(context.DisposalSlips.AsNoTracking(), scope, slip => slip.StockLocationId).Where(slip => ids.Contains(slip.Id)).ToListAsync(cancellationToken);
        return (stocktakes, adjustments, disposals);
    }

    /// <summary>
    /// Áp bộ lọc loại và mã phiếu trong database. Mã hiển thị là 8 ký tự hex cuối của Id (xem <see cref="StockDocumentCode.Of"/>),
    /// nên khớp bằng ILIKE trên 8 ký tự cuối của chuỗi Id.
    /// </summary>
    private static IQueryable<StocktakeSheet> Matching(IQueryable<StocktakeSheet> source, StockDocumentFilter filter) =>
        !filter.Includes(StockDocumentKind.Stocktake) ? source.Where(_ => false)
        : filter.CodeFragment is { } fragment ? source.Where(sheet => EF.Functions.ILike(sheet.Id.ToString().Substring(28), "%" + fragment + "%")) : source;

    private static IQueryable<QuantityAdjustment> Matching(IQueryable<QuantityAdjustment> source, StockDocumentFilter filter) =>
        !filter.Includes(StockDocumentKind.Adjustment) ? source.Where(_ => false)
        : filter.CodeFragment is { } fragment ? source.Where(item => EF.Functions.ILike(item.Id.ToString().Substring(28), "%" + fragment + "%")) : source;

    private static IQueryable<DisposalSlip> Matching(IQueryable<DisposalSlip> source, StockDocumentFilter filter) =>
        !filter.Includes(StockDocumentKind.Disposal) ? source.Where(_ => false)
        : filter.CodeFragment is { } fragment ? source.Where(slip => EF.Functions.ILike(slip.Id.ToString().Substring(28), "%" + fragment + "%")) : source;

    /// <summary>Giới hạn theo kho: null là mọi kho, một Id là chỉ kho đó (Guid.Empty không khớp kho nào).</summary>
    private static IQueryable<T> InScope<T>(IQueryable<T> source, Guid? scope, System.Linq.Expressions.Expression<Func<T, Guid>> location)
    {
        if (scope is not Guid only)
        {
            return source;
        }

        var body = System.Linq.Expressions.Expression.Equal(location.Body, System.Linq.Expressions.Expression.Constant(only));
        return source.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, location.Parameters));
    }

    public Task AddNoticeAsync(Notification notice, CancellationToken cancellationToken) =>
        context.Notifications.AddAsync(notice, cancellationToken).AsTask();

    public async Task<NoticePage> ListNoticesPageAsync(Guid recipientEmployeeId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var mine = context.Notifications.AsNoTracking().Where(notice => notice.RecipientEmployeeId == recipientEmployeeId);
        var total = await mine.CountAsync(cancellationToken);
        var size = Math.Max(1, pageSize);
        var items = await mine
            .OrderByDescending(notice => notice.CreatedAt)
            .ThenByDescending(notice => notice.Id)
            .Skip((Math.Max(1, pageNumber) - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new NoticePage(items, total);
    }

    public Task<int> CountUnseenNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken) =>
        context.Notifications.AsNoTracking()
            .CountAsync(notice => notice.RecipientEmployeeId == recipientEmployeeId && !notice.Seen, cancellationToken);

    public Task<Notification?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Notifications.FirstOrDefaultAsync(notice => notice.Id == id, cancellationToken);

    public async Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var notices = await context.Notifications.Where(notice => notice.DocumentId == documentId).ToListAsync(cancellationToken);
        context.Notifications.RemoveRange(notices);
    }

    /// <summary>Khóa dòng phiếu (SELECT ... FOR UPDATE) trong scope IUnitOfWork đang mở rồi mới đọc, để hai lần duyệt đồng thời chạy lần lượt.</summary>
    private async Task<T?> LockedAsync<T>(string table, Guid id, IQueryable<T> source, System.Linq.Expressions.Expression<Func<T, bool>> match, CancellationToken cancellationToken)
        where T : class
    {
        RequireOpenScope();
        await context.Database
            .SqlQuery<int>(System.Runtime.CompilerServices.FormattableStringFactory.Create("SELECT 1 AS \"Value\" FROM " + table + " WHERE id = {0} FOR UPDATE", id))
            .ToListAsync(cancellationToken);
        return await source.FirstOrDefaultAsync(match, cancellationToken);
    }

    public async Task<Guid?> FindDocumentLocationAsync(StockDocumentKind kind, Guid id, CancellationToken cancellationToken) =>
        kind switch
        {
            StockDocumentKind.Stocktake => await context.StocktakeSheets.AsNoTracking().Where(sheet => sheet.Id == id).Select(sheet => (Guid?)sheet.StockLocationId).FirstOrDefaultAsync(cancellationToken),
            StockDocumentKind.Adjustment => await context.QuantityAdjustments.AsNoTracking().Where(item => item.Id == id).Select(item => (Guid?)item.StockLocationId).FirstOrDefaultAsync(cancellationToken),
            StockDocumentKind.Disposal => await context.DisposalSlips.AsNoTracking().Where(slip => slip.Id == id).Select(slip => (Guid?)slip.StockLocationId).FirstOrDefaultAsync(cancellationToken),
            _ => null
        };

    /// <summary>Khóa dòng cần giao dịch đang mở (<c>IUnitOfWork.BeginAsync</c>); không có thì là lỗi lập trình, không tự mở giao dịch ngầm.</summary>
    private void RequireOpenScope()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Khóa dòng (FOR UPDATE) phải chạy trong một scope IUnitOfWork đang mở.");
        }
    }
}

public sealed class NoticeStore(PancrasDbContext context) : Pancras.Application.Notifications.INoticeStore
{
    public Task AddAsync(Notification notice, CancellationToken cancellationToken) =>
        context.Notifications.AddAsync(notice, cancellationToken).AsTask();

    public async Task RemoveForDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        context.Notifications.RemoveRange(await context.Notifications.Where(notice => notice.DocumentId == documentId).ToListAsync(cancellationToken));
}
