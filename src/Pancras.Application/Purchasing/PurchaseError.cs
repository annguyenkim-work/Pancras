namespace Pancras.Application.Common;

public enum PurchaseError
{
    None = 0,
    InvalidOrder,
    DuplicateCode,
    MissingMeasure,
    SplitGroup,
    AlreadyReceived,
    NegativeMoney,
    UngroupedCharge,
    ManualChargeLocked,
    AttachmentInvalid,
    AttachmentTooLarge,
    AttachmentLimit,
    AttachmentNotFound,
    ZeroPrice,
    ChargeFailed,

    /// <summary>Dữ liệu hợp lệ nhưng không ghi được (lỗi hệ thống hoặc có người khác vừa sửa). Khác với <see cref="InvalidOrder"/>.</summary>
    SaveFailed,
    InvalidQuantity,

    /// <summary>Đơn chưa duyệt, đã nhận đủ hoặc đã hủy nên không nhận hàng được.</summary>
    NotReceivable,

    /// <summary>Số thực nhận âm, nhận lố khi chưa phải lần cuối, hoặc không nhận món nào.</summary>
    InvalidReceiptQuantity,

    /// <summary>Lần nhận cuối lệch số đặt mà không ghi lý do.</summary>
    ReceiptNoteRequired,

    /// <summary>Trạng thái hiện tại không cho phép bước chuyển này.</summary>
    InvalidTransition,

    ReasonRequired,

    /// <summary>Lý do trả về nháp hoặc hủy dài hơn 500 ký tự.</summary>
    ReasonTooLong,

    /// <summary>Người khác đã lưu đơn sau khi form này được mở.</summary>
    VersionConflict,

    /// <summary>Số tiền điều chỉnh sau khi làm tròn không vừa cột numeric(18,0).</summary>
    AmountTooLarge,

    /// <summary>Đơn đã có lần nhận hàng hoặc đã hủy nên không sửa được nữa.</summary>
    NotEditable,

    /// <summary>Khoản điều chỉnh sau nhập không hợp lệ (đơn chưa nhận đủ, số tiền 0, thiếu tên hoặc lý do).</summary>
    AdjustmentInvalid,

    /// <summary>Vai trò hiện tại không được thực hiện thao tác này.</summary>
    Forbidden,

    /// <summary>Đơn đã hủy nên không nhận thêm file đính kèm.</summary>
    OrderCancelled,

    /// <summary>Có dòng nhận nhiều hơn số còn phải nhận. Detail: <c>mã SKU|số nhập|số còn phải nhận</c>.</summary>
    ReceiptExceedsOrdered
}
