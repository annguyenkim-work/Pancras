using Pancras.Application.Common;

namespace Pancras.Web.Pages.Purchasing;

public static class PurchaseErrorMessages
{
    public static string For(PurchaseError error) => error switch
    {
        PurchaseError.DuplicateCode => "Mã đơn đã được dùng.",
        PurchaseError.MissingMeasure => "Mặt hàng chưa có số đo để chia.",
        PurchaseError.AlreadyReceived => "Đơn đã nhập kho.",
        PurchaseError.NegativeMoney => "Số tiền không được âm.",
        PurchaseError.SplitGroup => "Khoản không được đứng ở hai nhóm.",
        PurchaseError.UngroupedCharge => "Còn khoản chi phí chưa gán nhóm. Gán nhóm cho mọi khoản trước khi nhập kho, nếu không khoản đó không được tính vào giá vốn.",
        PurchaseError.ManualChargeLocked => "Có khoản chi phí chia tay. Xóa khoản đó trước khi thêm hoặc bớt mặt hàng, rồi thêm lại sau khi sửa xong.",
        PurchaseError.ZeroPrice => "Mỗi mặt hàng cần có đơn giá FOB lớn hơn 0. Đơn giá 0 làm sai cách chia chi phí và kéo giá vốn xuống.",
        PurchaseError.ChargeFailed => "Đơn đã được tạo nhưng có khoản chi phí container chưa được thêm vào. Kiểm tra mục chi phí và thêm lại các khoản còn thiếu trước khi nhập kho.",
        PurchaseError.AttachmentInvalid => "File không hợp lệ. Chỉ nhận PDF, JPG, PNG, Excel, Word, và nội dung phải đúng định dạng.",
        PurchaseError.AttachmentTooLarge => "File quá lớn. Tối đa 10 MB mỗi file.",
        PurchaseError.AttachmentLimit => "Đơn đã đủ số file đính kèm tối đa (30).",
        PurchaseError.AttachmentNotFound => "Không tìm thấy chứng từ.",
        _ => "Đơn không được tạo."
    };
}
