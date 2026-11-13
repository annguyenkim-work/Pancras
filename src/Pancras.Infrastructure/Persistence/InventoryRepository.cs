using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;

namespace Pancras.Infrastructure.Persistence;

public sealed class InventoryRepository(PancrasDbContext context, ILogger<InventoryRepository> logger) : IInventoryRepository
{
    public async Task<IReadOnlyList<StockLocation>> ListLocationsAsync(CancellationToken cancellationToken) =>
        await context.StockLocations.AsNoTracking().OrderBy(location => location.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockItemChoice>> ListItemChoicesAsync(CancellationToken cancellationToken)
    {
        var items = await context.CatalogItems.AsNoTracking()
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Code)
            .Select(item => new StockItemChoice(item.Id, item.Code, item.Name, item.Unit))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(StockListFilter filter, CancellationToken cancellationToken)
    {
        if (filter.LocationRejected || filter.CategoryRejected)
        {
            return [];
        }

        var query =
            from balance in context.StockBalances.AsNoTracking()
            join item in context.CatalogItems.AsNoTracking() on balance.CatalogItemId equals item.Id
            join location in context.StockLocations.AsNoTracking() on balance.StockLocationId equals location.Id
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
        var missing = context.CatalogItems.AsNoTracking().Where(item => !item.IsCombo && !stocked.Contains(item.Id));
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

    public Task<TransferSlip?> FindTransferForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.TransferSlips.Include(slip => slip.Lines).FirstOrDefaultAsync(slip => slip.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TransferSlip>> ListOpenTransfersAsync(CancellationToken cancellationToken) =>
        await context.TransferSlips.AsNoTracking()
            .Include(slip => slip.Lines)
            .Where(slip => slip.Status == TransferStatus.Draft || slip.Status == TransferStatus.InTransit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TransferSlip>> ListTransfersAsync(CancellationToken cancellationToken) =>
        await context.TransferSlips.AsNoTracking()
            .Include(slip => slip.Lines)
            .OrderByDescending(slip => slip.Id)
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
            join location in context.StockLocations.AsNoTracking() on balance.StockLocationId equals location.Id
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
        context.StocktakeSheets.FirstOrDefaultAsync(sheet => sheet.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(CancellationToken cancellationToken) =>
        await context.StocktakeSheets.AsNoTracking().ToListAsync(cancellationToken);

    public Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken) =>
        context.QuantityAdjustments.AddAsync(adjustment, cancellationToken).AsTask();

    public Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.QuantityAdjustments.FirstOrDefaultAsync(adjustment => adjustment.Id == id, cancellationToken);

    public async Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(CancellationToken cancellationToken) =>
        await context.QuantityAdjustments.AsNoTracking().ToListAsync(cancellationToken);

    public Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken) =>
        context.DisposalSlips.AddAsync(slip, cancellationToken).AsTask();

    public Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.DisposalSlips.FirstOrDefaultAsync(slip => slip.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(CancellationToken cancellationToken) =>
        await context.DisposalSlips.AsNoTracking().ToListAsync(cancellationToken);

    public Task AddNoticeAsync(StockNotice notice, CancellationToken cancellationToken) =>
        context.StockNotices.AddAsync(notice, cancellationToken).AsTask();

    public async Task<IReadOnlyList<StockNotice>> ListNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken) =>
        await context.StockNotices.AsNoTracking()
            .Where(notice => notice.RecipientEmployeeId == recipientEmployeeId)
            .OrderByDescending(notice => notice.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<StockNotice?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.StockNotices.FirstOrDefaultAsync(notice => notice.Id == id, cancellationToken);

    public async Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var notices = await context.StockNotices.Where(notice => notice.DocumentId == documentId).ToListAsync(cancellationToken);
        context.StockNotices.RemoveRange(notices);
    }

    public async Task<bool> CommitAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Inventory commit failed.");
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return false;
        }
    }
}
