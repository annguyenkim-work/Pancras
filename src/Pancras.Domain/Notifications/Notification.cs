namespace Pancras.Domain.Notifications;

public enum NotificationKind
{
    DisposalApproved = 1,
    AdjustmentPending = 2,

    /// <summary>Thông báo bước của phiếu chuyển kho; nội dung cụ thể nằm ở <see cref="Notification.Step"/>.</summary>
    Transfer = 3,

    /// <summary>Thông báo bước của đơn mua; không gắn với kho cụ thể khi gửi cho người duyệt hoặc người lập đơn.</summary>
    Purchase = 4
}

public sealed class Notification
{
    public const string DisposalStep = "Đã duyệt hủy";

    public const string AdjustmentStep = "Chờ duyệt điều chỉnh";

    public const string TransferRequestedStep = "Phiếu đề nghị chuyển kho chờ duyệt";

    public const string TransferEditedStep = "Phiếu đề nghị chuyển kho vừa được sửa";

    public const string TransferApprovedStep = "Phiếu chuyển kho đã được duyệt";

    public const string TransferRejectedStep = "Phiếu chuyển kho bị từ chối";

    public const string TransferShippedStep = "Hàng chuyển kho đã xuất, đang trên đường";

    public const string TransferReceivedStep = "Hàng chuyển kho đã được nhận";

    public const string TransferDiscrepancyStep = "Nhận hàng chuyển kho có chênh lệch, chờ xử lý";

    public const string TransferResolvedStep = "Đã xử lý chênh lệch phiếu chuyển kho";

    public const string TransferCancelledStep = "Phiếu chuyển kho đã bị hủy";

    public const string TransferReturnedStep = "Hàng chuyển kho được trả về kho xuất";

    public const string PurchaseSubmittedStep = "Đơn mua chờ duyệt";

    public const string PurchaseApprovedStep = "Đơn mua đã được duyệt";

    public const string PurchaseReturnedStep = "Đơn mua bị trả về nháp";

    public const string PurchaseInTransitStep = "Đơn mua đang về";

    private Notification()
    {
        Step = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid RecipientEmployeeId { get; private set; }

    /// <summary>Kho liên quan; null khi thông báo không gắn kho (ví dụ đơn mua chờ duyệt).</summary>
    public Guid? StockLocationId { get; private set; }

    public Guid DocumentId { get; private set; }

    public NotificationKind Kind { get; private set; }

    public string Step { get; private set; }

    public bool Seen { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Notification Create(
        Guid recipientEmployeeId,
        Guid? stockLocationId,
        Guid documentId,
        NotificationKind kind,
        string step,
        DateTimeOffset createdAt)
    {
        if (recipientEmployeeId == Guid.Empty || stockLocationId == Guid.Empty || documentId == Guid.Empty)
        {
            throw new ArgumentException("Notice references are required.");
        }

        if (string.IsNullOrWhiteSpace(step))
        {
            throw new ArgumentException("Notice step is required.", nameof(step));
        }

        return new Notification
        {
            Id = Guid.CreateVersion7(),
            RecipientEmployeeId = recipientEmployeeId,
            StockLocationId = stockLocationId,
            DocumentId = documentId,
            Kind = kind,
            Step = step.Trim(),
            CreatedAt = createdAt
        };
    }

    public void MarkSeen() => Seen = true;
}
