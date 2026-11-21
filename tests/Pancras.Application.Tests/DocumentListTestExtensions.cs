using Pancras.Application.Inventory;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

/// <summary>Lấy trang đầu (50 dòng) của các danh sách phiếu, cho test chỉ quan tâm nội dung dòng và quyền.</summary>
internal static class DocumentListTestExtensions
{
    public static async Task<IReadOnlyList<TransferSlipRow>> ListTransfersAsync(this ListInventoryDocumentsHandler handler, EmployeeActor actor, CancellationToken cancellationToken) =>
        (await handler.ListTransferPageAsync(actor, new TransferListRequest(PageSize: 50), cancellationToken)).Rows;

    public static async Task<IReadOnlyList<StocktakeRow>> ListStocktakesAsync(this ListInventoryDocumentsHandler handler, EmployeeActor actor, CancellationToken cancellationToken) =>
        (await handler.ListStockDocumentPageAsync(actor, 1, 50, null, new StockDocumentFilter(), cancellationToken)).Stocktakes;

    public static async Task<IReadOnlyList<AdjustmentRow>> ListAdjustmentsAsync(this ListInventoryDocumentsHandler handler, EmployeeActor actor, CancellationToken cancellationToken) =>
        (await handler.ListStockDocumentPageAsync(actor, 1, 50, null, new StockDocumentFilter(), cancellationToken)).Adjustments;

    public static async Task<IReadOnlyList<DisposalRow>> ListDisposalsAsync(this ListInventoryDocumentsHandler handler, EmployeeActor actor, CancellationToken cancellationToken) =>
        (await handler.ListStockDocumentPageAsync(actor, 1, 50, null, new StockDocumentFilter(), cancellationToken)).Disposals;
}
