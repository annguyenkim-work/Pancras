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
        _ => "Đơn không được tạo."
    };
}
