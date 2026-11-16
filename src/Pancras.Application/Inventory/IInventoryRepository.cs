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

    Task<Guid?> FindTransferIdByRequestKeyAsync(Guid requestKey, Guid requestedById, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferSlip>> ListOpenTransfersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferSlip>> ListTransfersAsync(CancellationToken cancellationToken);

    Task AddTransferEventAsync(TransferEvent transferEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferEvent>> ListTransferEventsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken);

    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> ListHistoryAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken);

    Task<StockItemProfile?> FindItemProfileAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockBalanceRow>> ListItemBalancesAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> ListItemMovementsAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task AddStocktakeAsync(StocktakeSheet sheet, CancellationToken cancellationToken);

    Task<StocktakeSheet?> FindStocktakeForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(CancellationToken cancellationToken);

    Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken);

    Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(CancellationToken cancellationToken);

    Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken);

    Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(CancellationToken cancellationToken);

    Task AddNoticeAsync(Notification notice, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> ListNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken);

    Task<Notification?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Kho của một biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy. Chỉ đọc, không khóa dòng và không mở transaction,
    /// để các thao tác phụ (đính kèm, tải file) không để lại transaction treo.
    /// </summary>
    Task<Guid?> FindDocumentLocationAsync(StockDocumentKind kind, Guid id, CancellationToken cancellationToken);

    Task<bool> CommitAsync(CancellationToken cancellationToken);
}
