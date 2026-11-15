namespace Pancras.Domain.Inventory;

public enum TransferEventKind
{
    Requested = 1,
    Edited = 2,
    Approved = 3,
    Rejected = 4,
    Shipped = 5,
    Received = 6,
    ReceivedWithDiscrepancy = 7,
    Resolved = 8,
    Cancelled = 9,
    Returned = 10
}

/// <summary>
/// Một dòng lịch sử của phiếu chuyển: ai làm gì, lúc nào, kèm lý do nếu có (từ chối, hủy, trả hàng, chênh lệch).
/// Chỉ ghi thêm, không sửa.
/// </summary>
public sealed class TransferEvent
{
    public const int ActorMaxLength = 64;

    public const int NoteMaxLength = 500;

    private TransferEvent()
    {
        ActorCode = string.Empty;
        Note = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TransferSlipId { get; private set; }

    public TransferEventKind Kind { get; private set; }

    /// <summary>Mã nhân viên thao tác; rỗng nếu không rõ.</summary>
    public string ActorCode { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Note { get; private set; }

    public static TransferEvent Create(Guid transferSlipId, TransferEventKind kind, string? actorCode, DateTimeOffset occurredAt, string? note)
    {
        if (transferSlipId == Guid.Empty)
        {
            throw new ArgumentException("Transfer slip is required.", nameof(transferSlipId));
        }

        var actor = actorCode?.Trim() ?? string.Empty;
        var text = note?.Trim() ?? string.Empty;
        return new TransferEvent
        {
            Id = Guid.CreateVersion7(),
            TransferSlipId = transferSlipId,
            Kind = kind,
            ActorCode = actor.Length <= ActorMaxLength ? actor : actor[..ActorMaxLength],
            OccurredAt = occurredAt,
            Note = text.Length <= NoteMaxLength ? text : text[..NoteMaxLength]
        };
    }
}
