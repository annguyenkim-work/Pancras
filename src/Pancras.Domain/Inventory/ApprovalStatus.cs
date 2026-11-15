namespace Pancras.Domain.Inventory;

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,

    /// <summary>Phiếu chỉ ghi nhận chênh lệch (ví dụ khi nhận hàng chuyển kho thiếu/thừa); tồn đã đúng thực tế nên không cộng trừ lần nữa.</summary>
    Recorded = 3
}
