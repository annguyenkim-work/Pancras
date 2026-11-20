using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public interface IPurchaseOrderRepository
{
    /// <summary>Số container và vận đơn của mọi đơn (chỉ các cột này, không nạp dòng hàng), để cảnh báo trùng lô hàng.</summary>
    Task<IReadOnlyList<PurchaseShipmentKey>> ListShipmentKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Một trang đơn mua (kèm dòng hàng và chi phí) đã lọc theo quyền xem, nhà cung cấp, từ khóa; cắt trang và đếm trong database.
    /// Số trang vượt cuối thì lấy trang cuối.
    /// </summary>
    Task<PurchaseOrderPage> ListPageAsync(PurchaseOrderQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken);
}
