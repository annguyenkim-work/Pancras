using Microsoft.EntityFrameworkCore;
using Pancras.Application.Suppliers;
using Pancras.Domain.Purchasing;

namespace Pancras.Infrastructure.Persistence;

/// <summary>Thống kê đơn mua theo nhà cung cấp cho thẻ nhà cung cấp: tổng số đơn và số đơn chưa nhập kho.</summary>
public sealed class PurchaseSupplierStatsReader(PancrasDbContext context) : ISupplierPurchaseStatsReader
{
    public async Task<IReadOnlyDictionary<Guid, SupplierPurchaseStats>> ReadAsync(
        IReadOnlyCollection<Guid> supplierIds,
        CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0)
        {
            return new Dictionary<Guid, SupplierPurchaseStats>();
        }

        var ids = supplierIds.ToArray();
        var rows = await context.PurchaseOrders.AsNoTracking()
            .Where(order => ids.Contains(order.SupplierId))
            .GroupBy(order => order.SupplierId)
            .Select(group => new
            {
                SupplierId = group.Key,
                Total = group.Count(),
                Open = group.Count(order => order.Status == PurchaseStatus.Open)
            })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.SupplierId, row => new SupplierPurchaseStats(row.Total, row.Open));
    }
}
