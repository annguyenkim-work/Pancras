namespace Pancras.Domain.Inventory;

public enum StockNoticeKind
{
    DisposalApproved = 1,
    AdjustmentPending = 2
}

public sealed class StockNotice
{
    public const string DisposalStep = "Đã duyệt hủy";

    public const string AdjustmentStep = "Chờ duyệt điều chỉnh";

    private StockNotice()
    {
        Step = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid RecipientEmployeeId { get; private set; }

    public Guid StockLocationId { get; private set; }

    public Guid DocumentId { get; private set; }

    public StockNoticeKind Kind { get; private set; }

    public string Step { get; private set; }

    public bool Seen { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static StockNotice Create(
        Guid recipientEmployeeId,
        Guid stockLocationId,
        Guid documentId,
        StockNoticeKind kind,
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

        return new StockNotice
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
