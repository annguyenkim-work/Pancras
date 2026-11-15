using Pancras.Application.Common;

namespace Pancras.Web.Pages.Purchasing;

public static class PurchaseErrorMessages
{
    /// <summary>Câu cho InvalidOrder khi tạo đơn mới.</summary>
    public const string CreateFailed = "Đơn không được tạo. Kiểm tra mã đơn, nhà cung cấp, kho nhận và các mặt hàng.";

    /// <summary>Câu cho InvalidOrder khi sửa đơn.</summary>
    public const string UpdateFailed = "Không lưu được thay đổi. Kiểm tra nhà cung cấp, kho nhận và các mặt hàng.";

    private const string Generic = "Không thực hiện được thao tác này. Đơn có thể đã bị xóa hoặc số liệu không hợp lệ. Tải lại trang rồi thử lại.";

    /// <param name="invalidOrderText">Câu riêng cho InvalidOrder theo thao tác (tạo, sửa); bỏ trống thì dùng câu chung.</param>
    public static string For(PurchaseError error, string? invalidOrderText = null) => error switch
    {
        PurchaseError.InvalidOrder => invalidOrderText ?? Generic,
        PurchaseError.SaveFailed => "Không lưu được do lỗi hệ thống hoặc có người khác vừa sửa đơn này. Tải lại trang rồi thử lại. Nếu vẫn lỗi, báo quản trị viên (lỗi đã được ghi vào nhật ký máy chủ).",
        PurchaseError.DuplicateCode => "Mã đơn đã được dùng.",
        PurchaseError.MissingMeasure => "Có mặt hàng chưa khai báo thể tích hoặc cân nặng ở Danh mục nên không chia theo cách này được. Chọn cách chia \"Theo trị giá FOB\" hoặc \"Theo số lượng\", hoặc bổ sung số đo cho mặt hàng.",
        PurchaseError.AlreadyReceived => "Đơn đã nhập kho.",
        PurchaseError.NegativeMoney => "Số tiền không được âm.",
        PurchaseError.SplitGroup => "Khoản không được đứng ở hai nhóm.",
        PurchaseError.UngroupedCharge => "Còn khoản chi phí chưa gán nhóm. Gán nhóm cho mọi khoản trước khi nhập kho, nếu không khoản đó không được tính vào giá vốn.",
        PurchaseError.ManualChargeLocked => "Có khoản chi phí chia tay. Xóa khoản đó trước khi thêm hoặc bớt mặt hàng, rồi thêm lại sau khi sửa xong.",
        PurchaseError.ZeroPrice => "Mỗi mặt hàng cần có đơn giá FOB lớn hơn 0. Đơn giá 0 làm sai cách chia chi phí và kéo giá vốn xuống.",
        PurchaseError.InvalidQuantity => "Số lượng mỗi mặt hàng phải lớn hơn 0. Muốn bỏ một mặt hàng khỏi đơn, bấm nút xóa ở cuối dòng đó.",
        PurchaseError.ChargeFailed => "Có khoản chi phí container không thêm được, đơn chưa được tạo. Kiểm tra số tiền và cách chia rồi thử lại.",
        PurchaseError.AttachmentInvalid => "File không hợp lệ. Chỉ nhận PDF, JPG, PNG, Excel (xlsx), Word (docx), và nội dung phải đúng định dạng.",
        PurchaseError.AttachmentTooLarge => "File quá lớn. Tối đa 10 MB mỗi file.",
        PurchaseError.AttachmentLimit => "Đơn đã đủ số file đính kèm tối đa (30).",
        PurchaseError.NotReceivable => "Đơn chưa được duyệt, đã nhận đủ hoặc đã hủy nên chưa nhận hàng được.",
        PurchaseError.InvalidReceiptQuantity => "Số thực nhận không hợp lệ.",
        PurchaseError.ReceiptNoteRequired => "Lần nhận cuối lệch số đặt: cần ghi lý do.",
        PurchaseError.InvalidTransition => "Trạng thái hiện tại của đơn không cho phép bước này. Tải lại trang để xem trạng thái mới nhất.",
        PurchaseError.ReasonRequired => "Cần ghi lý do.",
        PurchaseError.ReasonTooLong => "Lý do chỉ được tối đa 500 ký tự.",
        PurchaseError.VersionConflict => "Đơn vừa được người khác sửa. Tải lại và thử lại.",
        PurchaseError.AmountTooLarge => "Số tiền vượt quá giới hạn cho phép.",
        PurchaseError.NotEditable => "Đơn đã có lần nhận hàng hoặc đã hủy nên không sửa được nữa.",
        PurchaseError.AdjustmentInvalid => "Khoản điều chỉnh không hợp lệ: đơn phải nhận đủ, số tiền khác 0, có tên khoản và lý do, và chọn cách chia khác \"Nhập tay\".",
        PurchaseError.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
        PurchaseError.OrderCancelled => "Đơn đã hủy nên không thêm file đính kèm được.",
        PurchaseError.AttachmentNotFound => "Không tìm thấy chứng từ.",
        _ => Generic
    };
}
