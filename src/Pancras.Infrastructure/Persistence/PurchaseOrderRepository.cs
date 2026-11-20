using Microsoft.EntityFrameworkCore;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Infrastructure.Persistence;

public sealed class PurchaseOrderRepository(PancrasDbContext context) : IPurchaseOrderRepository
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

    public Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken) =>
        context.PurchaseOrders.AddAsync(order, cancellationToken).AsTask();
}
