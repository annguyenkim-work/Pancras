using System.Linq.Expressions;
using Pancras.Domain.Notifications;
using Pancras.Application.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public interface IInventoryRepository
{
    Task<IReadOnlyList<StockLocation>> ListLocationsAsync(CancellationToken cancellationToken);

    /// <summary>Kho đã xóa mềm. Chỉ để hiển thị tên trên phiếu và lịch sử cũ, không dùng để chọn kho.</summary>
    Task<IReadOnlyList<StockLocation>> ListDeletedLocationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StockItemChoice>> ListItemChoicesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(ListStockBalancesQuery filter, CancellationToken cancellationToken);

    Task<StockBalance?> FindBalanceForUpdateAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken);

    Task<int> SumOnHandAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken);

    Task AddTransferAsync(TransferSlip slip, CancellationToken cancellationToken);

    Task<TransferSlip?> FindTransferForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc phiếu chuyển không khóa dòng và không cần scope: cho kiểm tra trước, tải tệp đính kèm, lưu tệp (không ghi vào phiếu).</summary>
    Task<TransferSlip?> FindTransferReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid?> FindTransferIdByRequestKeyAsync(Guid requestKey, Guid requestedById, CancellationToken cancellationToken);

    /// <summary>
    /// Một trang phiếu chuyển (kèm dòng hàng) đã lọc, sắp xếp (đang xử lý trước, rồi mới nhất trước), cắt trang và đếm trong database.
    /// Số trang vượt cuối thì lấy trang cuối.
    /// </summary>
    Task<TransferPage> ListTransfersPageAsync(TransferListQuery query, int pageNumber, int pageSize, CancellationToken cancellationToken);

    /// <summary>Số phiếu thỏa điều kiện (đã gồm quyền xem do người gọi ghép vào), cho các nhãn đếm trên giao diện.</summary>
    Task<int> CountTransfersAsync(Expression<Func<TransferSlip, bool>> predicate, CancellationToken cancellationToken);

    /// <summary>Các phiếu theo Id mà người xem được thấy (kèm dòng hàng). Chỉ đọc.</summary>
    Task<IReadOnlyList<TransferSlip>> FindTransfersAsync(Expression<Func<TransferSlip, bool>> visible, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Số thứ tự (mã CK-000123) của các phiếu chuyển; phiếu không tồn tại thì không có trong kết quả. Chỉ đọc.</summary>
    Task<IReadOnlyDictionary<Guid, int>> ListTransferNumbersAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken);

    Task AddTransferEventAsync(TransferEvent transferEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferEvent>> ListTransferEventsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken);

    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> ListHistoryAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken);

    Task<StockItemProfile?> FindItemProfileAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockBalanceRow>> ListItemBalancesAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> ListItemMovementsAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task AddStocktakeAsync(StocktakeSheet sheet, CancellationToken cancellationToken);

    Task<StocktakeSheet?> FindStocktakeForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken);

    Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken);

    Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken);


    // Biên bản kiểm kê, phiếu điều chỉnh, phiếu hủy. scope: null là mọi kho; một Id là chỉ kho đó (Guid.Empty: không kho nào), như InventoryAccess.Scope.

    /// <summary>Số phiếu từng loại trong phạm vi kho và khớp bộ lọc.</summary>
    Task<StockDocumentCounts> CountStockDocumentsAsync(Guid? scope, StockDocumentFilter filter, CancellationToken cancellationToken);

    /// <summary>Một đoạn biên bản kiểm kê trong phạm vi kho, mới nhất trước.</summary>
    Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken);

    /// <summary>Một đoạn phiếu điều chỉnh trong phạm vi kho, mới nhất trước.</summary>
    Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken);

    /// <summary>Một đoạn phiếu hủy trong phạm vi kho, mới nhất trước.</summary>
    Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(Guid? scope, StockDocumentRange range, StockDocumentFilter filter, CancellationToken cancellationToken);

    /// <summary>Loại và thứ tự (mới nhất trước) của một phiếu trong phạm vi kho; không có hoặc ngoài phạm vi thì null.</summary>
    Task<StockDocumentPosition?> LocateStockDocumentAsync(Guid? scope, Guid id, StockDocumentFilter filter, CancellationToken cancellationToken);

    /// <summary>Các biên bản, phiếu điều chỉnh, phiếu hủy theo Id trong phạm vi kho (để ghi chú lịch sử tồn). Chỉ đọc.</summary>
    Task<(IReadOnlyList<StocktakeSheet> Stocktakes, IReadOnlyList<QuantityAdjustment> Adjustments, IReadOnlyList<DisposalSlip> Disposals)> FindStockDocumentsAsync(Guid? scope, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task AddNoticeAsync(Notification notice, CancellationToken cancellationToken);

    /// <summary>Một trang thông báo của người nhận, cắt ở truy vấn: mới nhất trước (theo thời điểm tạo), không phụ thuộc đã xem hay chưa. <c>pageNumber</c> bắt đầu từ 1.</summary>
    Task<NoticePage> ListNoticesPageAsync(Guid recipientEmployeeId, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<int> CountUnseenNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken);

    Task<Notification?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Kho của một biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy. Chỉ đọc, không khóa dòng và không mở transaction,
    /// để các thao tác phụ (đính kèm, tải file) không để lại transaction treo.
    /// </summary>
    Task<Guid?> FindDocumentLocationAsync(StockDocumentKind kind, Guid id, CancellationToken cancellationToken);

}
