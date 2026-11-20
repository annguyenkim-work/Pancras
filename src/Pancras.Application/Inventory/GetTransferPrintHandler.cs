using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Dữ liệu cho bản in phiếu xuất kho. Dùng lại dòng phiếu của màn Kho nên không có giá vốn; phiếu ngoài phạm vi người xem coi như không tồn tại.
/// </summary>
public sealed class GetTransferPrintHandler(ListInventoryDocumentsHandler documents)
{
    public async Task<Result<TransferSlipRow, StockError>> HandleAsync(GetTransferPrintQuery query, CancellationToken cancellationToken)
    {
        var rows = await documents.GetTransfersAsync(query.Actor, [query.TransferSlipId], cancellationToken);
        var row = rows.FirstOrDefault(item => item.Id == query.TransferSlipId);
        if (row is null)
        {
            return Result<TransferSlipRow, StockError>.Fail(StockError.TransferNotFound);
        }

        if (!TransferSlip.CanPrint(row.Status))
        {
            return Result<TransferSlipRow, StockError>.Fail(StockError.TransferNotPrintable);
        }

        return row.Flow is { CanPrint: true }
            ? Result<TransferSlipRow, StockError>.Ok(row)
            : Result<TransferSlipRow, StockError>.Fail(StockError.Forbidden);
    }
}
