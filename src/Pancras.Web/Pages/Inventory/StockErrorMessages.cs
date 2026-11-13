using Pancras.Application.Common;

namespace Pancras.Web.Pages.Inventory;

public static class StockErrorMessages
{
    public static string? Describe(StockError error) => error switch
    {
        StockError.InvalidTransfer => "Phiếu chuyển không được tạo.",
        StockError.CancelRejected => "Không hủy được phiếu: phiếu không còn ở trạng thái đang chuyển.",
        StockError.ShipmentRejected => "Không đủ hàng để xuất.",
        StockError.MissingEvidence => "Cần lý do và chứng từ.",
        StockError.ApprovalForbidden => "Bạn không được duyệt phiếu này.",
        StockError.Forbidden => "Bạn không có quyền thao tác với kho này.",
        StockError.NegativeStock => "Tồn không được âm.",
        StockError.DisposalRejected => "Tồn không được âm.",
        _ => null
    };
}
