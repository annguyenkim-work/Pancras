using Pancras.Application.Common;

namespace Pancras.Web.Pages.Inventory;

public static class StockErrorMessages
{
    public static string? Describe(StockError error) => error switch
    {
        StockError.InvalidTransfer => "Phiếu chuyển không được tạo.",
        StockError.CancelRejected => "Không hủy được phiếu: phiếu không còn ở trạng thái đang chuyển.",
        StockError.ReasonRequired => "Cần nhập lý do (số thực nhận khác số xuất, hoặc nội dung xử lý chênh lệch).",
        StockError.AttachmentRequired => "Số thực nhận khác số xuất: cần đính kèm ít nhất một file (ảnh hàng, biên bản).",
        StockError.ApprovalRejected => "Không thực hiện được: phiếu không ở trạng thái phù hợp.",
        StockError.TransferNotFound => "Không tìm thấy phiếu chuyển.",
        StockError.AttachmentInvalid => "File không hợp lệ. Chỉ nhận PDF, JPG, PNG, Excel, Word.",
        StockError.AttachmentTooLarge => "File quá lớn (tối đa 10 MB).",
        StockError.AttachmentLimit => "Phiếu đã có quá nhiều file đính kèm.",
        StockError.ShipmentRejected => "Không đủ hàng để xuất.",
        StockError.MissingEvidence => "Cần lý do và chứng từ.",
        StockError.ApprovalForbidden => "Bạn không được duyệt phiếu này.",
        StockError.Forbidden => "Bạn không có quyền thao tác với kho này.",
        StockError.NegativeStock => "Tồn không được âm.",
        StockError.DisposalRejected => "Tồn không được âm.",
        _ => null
    };
}
