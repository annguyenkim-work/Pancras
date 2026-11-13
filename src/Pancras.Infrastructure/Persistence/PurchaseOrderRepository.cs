using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Infrastructure.Persistence;

public sealed class PurchaseOrderRepository(PancrasDbContext context, ILogger<PurchaseOrderRepository> logger) : IPurchaseOrderRepository
{
    public async Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken) =>
        await context.PurchaseOrders.AsNoTracking()
            .Include(order => order.Lines)
            .Include(order => order.Charges).ThenInclude(charge => charge.Shares)
            .OrderByDescending(order => order.Id)
            .ToListAsync(cancellationToken);

    public Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        context.PurchaseOrders.AsNoTracking()
            .Include(order => order.Lines)
            .Include(order => order.Charges).ThenInclude(charge => charge.Shares)
            .Include(order => order.Events)
            .Include(order => order.Adjustments)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.PurchaseOrders
            .Include(order => order.Lines)
            .Include(order => order.Charges).ThenInclude(charge => charge.Shares)
            .Include(order => order.Events)
            .Include(order => order.Adjustments)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        context.PurchaseOrders.AsNoTracking().AnyAsync(order => order.NormalizedCode == normalizedCode, cancellationToken);

    public async Task<bool> TryAddAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        await context.PurchaseOrders.AddAsync(order, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.Entry(order).State = EntityState.Detached;
            logger.LogWarning("Purchase order code collided.");
            return false;
        }
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
            logger.LogError(exception, "Purchase commit failed.");
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
