using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

internal static class StockDecisionErrors
{
    /// <summary>Đổi quyết định của domain thành mã lỗi, mỗi nguyên nhân một mã để thông báo đúng ý.</summary>
    public static StockError ToError(StockDecision decision) => decision switch
    {
        StockDecision.WrongRole => StockError.ApprovalForbidden,
        StockDecision.MissingEvidence => StockError.MissingEvidence,
        StockDecision.TooLong => StockError.EvidenceTooLong,
        StockDecision.InvalidQuantity => StockError.InvalidQuantity,
        StockDecision.Rejected => StockError.MissingTarget,
        StockDecision.NotPending => StockError.TransferStateChanged,
        _ => StockError.NegativeStock
    };

    /// <summary>Số liệu kèm theo thông báo không đủ tồn khả dụng, lấy từ kho tại thời điểm kiểm tra.</summary>
    public static string AvailabilityDetail(StockBalance? balance) =>
        balance is null ? "Còn 0, đang giữ chỗ 0" : $"Còn {balance.Available}, đang giữ chỗ {balance.Reserved}";
}
