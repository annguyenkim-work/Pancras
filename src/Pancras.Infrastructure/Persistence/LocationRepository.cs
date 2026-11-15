using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pancras.Application.Locations;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Infrastructure.Persistence;

public sealed class LocationRepository(PancrasDbContext context) : ILocationRepository, ILocationManagementRepository
{
    public async Task<IReadOnlyList<StockLocation>> ListAsync(CancellationToken cancellationToken) =>
        await context.StockLocations.AsNoTracking().OrderBy(location => location.Name).ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.StockLocations.AsNoTracking().AnyAsync(location => location.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        context.StockLocations.AsNoTracking()
            .AnyAsync(location => location.Name.ToLower() == name.ToLower(), cancellationToken);

    public async Task<bool> TryAddAsync(StockLocation location, CancellationToken cancellationToken)
    {
        await context.StockLocations.AddAsync(location, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(location).State = EntityState.Detached;
            return false;
        }
    }


    public Task<StockLocation?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.StockLocations.FirstOrDefaultAsync(location => location.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid excludingId, CancellationToken cancellationToken) =>
        context.StockLocations.AsNoTracking()
            .AnyAsync(location => location.Id != excludingId && location.Name.ToLower() == name.ToLower(), cancellationToken);

    public Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken) =>
        context.StockBalances.AsNoTracking()
            .AnyAsync(balance => balance.StockLocationId == id && (balance.OnHand != 0 || balance.Reserved != 0 || balance.InTransit != 0), cancellationToken);

    public Task<bool> HasActiveEmployeesAsync(Guid id, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .AnyAsync(employee => employee.StockLocationId == id && employee.IsActive, cancellationToken);

    public async Task<bool> HasOpenDocumentsAsync(Guid id, CancellationToken cancellationToken)
    {
        var openOrder = await context.PurchaseOrders.AsNoTracking()
            .AnyAsync(
                order => order.ReceiveLocationId == id
                    && (order.Status == PurchaseStatus.Draft
                        || order.Status == PurchaseStatus.PendingApproval
                        || order.Status == PurchaseStatus.Approved
                        || order.Status == PurchaseStatus.InTransit
                        || order.Status == PurchaseStatus.PartiallyReceived),
                cancellationToken);
        if (openOrder)
        {
            return true;
        }

        return await context.TransferSlips.AsNoTracking()
            .AnyAsync(
                slip => (slip.SourceLocationId == id || slip.DestinationLocationId == id)
                    && (slip.Status == TransferStatus.Draft
                        || slip.Status == TransferStatus.Requested
                        || slip.Status == TransferStatus.Approved
                        || slip.Status == TransferStatus.InTransit
                        || slip.Status == TransferStatus.ReceivedWithDiscrepancy),
                cancellationToken);
    }

    public async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
