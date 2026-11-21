using Microsoft.EntityFrameworkCore;
using Pancras.Application.Inventory;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Infrastructure.Persistence;

public sealed class PurchaseOrderRepository(PancrasDbContext context) : IPurchaseOrderRepository
{
    public async Task<IReadOnlyList<PurchaseShipmentKey>> ListShipmentKeysAsync(CancellationToken cancellationToken) =>
        await context.PurchaseOrders.AsNoTracking()
            .Select(order => new PurchaseShipmentKey(order.Id, order.Code, order.ContainerNumber, order.BillOfLading, order.Status))
            .ToListAsync(cancellationToken);

    public async Task<PurchaseOrderPage> ListPageAsync(PurchaseOrderQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var size = Math.Max(1, pageSize);
        var visible = context.PurchaseOrders.AsNoTracking().Where(filter.Visible);
        var anyVisible = await visible.AnyAsync(cancellationToken);
        var query = visible;
        if (filter.SupplierId is Guid supplierId)
        {
            query = query.Where(order => order.SupplierId == supplierId);
        }

        if (filter.Status is PurchaseStatus status)
        {
            query = query.Where(order => order.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var pattern = InventoryTextSearch.ContainsPattern(filter.Term);
            // Tên nhà cung cấp: ưu tiên bản chụp lúc lập đơn; đơn cũ chưa có bản chụp thì lấy tên hiện tại.
            query = query.Where(order =>
                EF.Functions.ILike(order.Code, pattern)
                || EF.Functions.ILike(order.ContainerNumber, pattern)
                || EF.Functions.ILike(order.BillOfLading, pattern)
                || EF.Functions.ILike(order.Port, pattern)
                || (order.SupplierName != "" && EF.Functions.ILike(order.SupplierName, pattern))
                || (order.SupplierName == "" && context.Suppliers.Any(supplier => supplier.Id == order.SupplierId && EF.Functions.ILike(supplier.Name, pattern))));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(pageNumber, 1, Math.Max(1, (total + size - 1) / size));
        var firstId = await query.OrderByDescending(order => order.Id).Select(order => (Guid?)order.Id).FirstOrDefaultAsync(cancellationToken);
        var items = await query
            .OrderByDescending(order => order.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Include(order => order.Lines)
            .Include(order => order.Charges).ThenInclude(charge => charge.Shares)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return new PurchaseOrderPage(items, total, page, anyVisible, firstId);
    }

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
