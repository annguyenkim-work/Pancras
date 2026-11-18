using Pancras.Application.Common;

namespace Pancras.Web.Pages.Inventory;

public static class StockErrorMessages
{
    /// <param name="error">Mã lỗi nghiệp vụ.</param>
    /// <param name="detail">Chi tiết đi kèm (ví dụ các mã SKU thiếu tồn), có thể null.</param>
    public static string? Describe(StockError error, string? detail = null) => error switch
    {
        StockError.InvalidTransfer => "Phiếu chuyển không hợp lệ: cần chọn kho xuất khác kho nhận và ít nhất một mặt hàng có số lượng lớn hơn 0.",
        StockError.NotYourLocation => "Kho xuất hoặc kho nhận phải là kho/showroom của bạn.",
        StockError.NotCreator => "Chỉ người lập phiếu mới được sửa hoặc hủy phiếu này. Bạn có thể duyệt hoặc từ chối nếu có quyền.",
        StockError.TransferStateChanged => "Phiếu vừa được người khác xử lý hoặc đã đổi trạng thái. Tải lại trang để xem trạng thái mới nhất.",
        StockError.InsufficientStock => string.IsNullOrWhiteSpace(detail)
            ? "Kho xuất không đủ tồn khả dụng cho phiếu này."
            : "Kho xuất không đủ tồn khả dụng: " + detail + ".",
        StockError.QuantityRequired => "Nhập số thực nhận cho từng mặt hàng, kể cả khi nhận đủ.",
        StockError.InvalidText => "Nội dung nhập quá dài: lý do tối đa 500 ký tự, số seal tối đa 100, người vận chuyển tối đa 200.",
        StockError.ReturnRejected => "Không trả được hàng về kho xuất: phiếu không còn đang vận chuyển hoặc tồn đang chuyển ở kho nhận không khớp.",
        StockError.CancelRejected => "Không hủy được phiếu: chỉ hủy được khi chưa xuất kho.",
        StockError.ReasonRequired => "Cần nhập lý do cho thao tác này (từ chối, hủy, trả hàng, chênh lệch khi nhận hoặc cách xử lý chênh lệch).",
        StockError.AttachmentRequired => "Số thực nhận khác số xuất: cần đính kèm ít nhất một file (ảnh hàng, biên bản).",
        StockError.ApprovalRejected => "Không thực hiện được: phiếu không ở trạng thái phù hợp.",
        StockError.ReceiptRejected => "Không nhận được hàng: kiểm tra lại số thực nhận của từng mặt hàng.",
        StockError.TransferNotFound => "Không tìm thấy phiếu chuyển.",
        StockError.TransferNotPrintable => "Chỉ in được phiếu đã duyệt trở đi (không in phiếu chờ duyệt, bị từ chối hoặc đã hủy).",
        StockError.AttachmentInvalid => "File không hợp lệ. Chỉ nhận PDF, JPG, PNG, Excel, Word.",
        StockError.AttachmentTooLarge => "File quá lớn (tối đa 10 MB).",
        StockError.AttachmentLimit => "Phiếu đã có quá nhiều file đính kèm.",
        StockError.ShipmentRejected => "Không đủ hàng để xuất.",
        StockError.MissingEvidence => "Cần lý do và chứng từ.",
        StockError.EvidenceTooLong => "Lý do hoặc chứng từ quá dài.",
        StockError.MissingTarget => "Thiếu địa điểm hoặc mặt hàng.",
        StockError.InvalidQuantity => "Số lượng không hợp lệ.",
        StockError.CountRequired => "Số đếm chưa được nhập.",
        StockError.InvalidCount => "Số đếm phải là số nguyên không âm.",
        StockError.ApprovalForbidden => "Bạn không được duyệt phiếu này.",
        StockError.Forbidden => "Bạn không có quyền thao tác với kho này.",
        StockError.NegativeStock => NotEnoughAvailable(detail),
        StockError.DisposalRejected => NotEnoughAvailable(detail),
        _ => null
    };

    private static string NotEnoughAvailable(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? "Không đủ tồn khả dụng." : "Không đủ tồn khả dụng. " + detail + ".";
}
