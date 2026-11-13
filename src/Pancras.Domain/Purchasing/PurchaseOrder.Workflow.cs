namespace Pancras.Domain.Purchasing;

public enum PurchaseEventKind
{
    Created = 1,
    Submitted = 2,
    Approved = 3,
    ReturnedToDraft = 4,
    InTransit = 5,
    PartiallyReceived = 6,
    Received = 7,
    Cancelled = 8,
    CostAdjusted = 9
}

/// <summary>Một mốc trong lịch sử đơn mua: ai làm gì, lúc nào.</summary>
public sealed class PurchaseEvent
{
    public const int NoteMaxLength = 500;

    private PurchaseEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid PurchaseOrderId { get; private set; }

    public PurchaseEventKind Kind { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Mã nhân viên thao tác; rỗng nếu không rõ.</summary>
    public string ActorCode { get; private set; } = string.Empty;

    public string Note { get; private set; } = string.Empty;

    internal static PurchaseEvent Create(Guid orderId, PurchaseEventKind kind, DateTimeOffset at, string actor, string? note)
    {
        var trimmed = note?.Trim() ?? string.Empty;
        return new PurchaseEvent
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = orderId,
            Kind = kind,
            OccurredAt = at,
            ActorCode = actor,
            Note = trimmed.Length <= NoteMaxLength ? trimmed : trimmed[..NoteMaxLength]
        };
    }
}

/// <summary>
/// Điều chỉnh chi phí sau khi đơn đã nhận đủ (hóa đơn về muộn, tờ khai điều chỉnh). Số tiền có dấu: dương là tăng giá vốn.
/// Phần ứng với hàng còn tồn được cộng vào giá vốn bình quân; phần ứng với hàng đã xuất là chênh lệch, không sửa ngược.
/// </summary>
public sealed class PurchaseCostAdjustment
{
    public const int ReasonMaxLength = 500;

    private PurchaseCostAdjustment()
    {
    }

    public Guid Id { get; private set; }

    public Guid PurchaseOrderId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public ChargeSplitMethod Method { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Phần đã cộng vào giá vốn hàng còn tồn.</summary>
    public decimal AppliedToStock { get; private set; }

    /// <summary>Phần ứng với hàng đã xuất (chênh lệch giá vốn).</summary>
    public decimal Variance => Amount - AppliedToStock;

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    internal static PurchaseCostAdjustment Create(Guid orderId, string label, decimal amount, ChargeSplitMethod method, string reason, decimal appliedToStock, DateTimeOffset at, string by) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = orderId,
            Label = label,
            Amount = amount,
            Method = method,
            Reason = reason,
            AppliedToStock = appliedToStock,
            CreatedAt = at,
            CreatedBy = by
        };
}

public enum ReceiptDecision
{
    Ok = 0,
    WrongStatus,

    /// <summary>Số âm, nhận lố khi chưa phải lần cuối, hoặc không có gì để nhận.</summary>
    InvalidQuantity,

    /// <summary>Lần nhận cuối lệch với số đặt thì phải có ghi chú lý do.</summary>
    NoteRequired
}

/// <summary>Kết quả tính cho một dòng trong một lần nhận.</summary>
/// <param name="Quantity">Số lượng nhận lần này.</param>
/// <param name="UnitCost">Giá vốn đơn vị của số lượng nhận lần này (đồng).</param>
/// <param name="Revaluation">Giá vốn còn lại của dòng phải dồn vào hàng đã nhận trước đó (chỉ khi đóng đơn mà lần này không nhận thêm).</param>
/// <param name="Unreceived">Giá vốn của dòng không nhận được món nào: không vào tồn kho.</param>
public sealed record ReceiptLineResult(PurchaseLine Line, int Quantity, decimal UnitCost, decimal Revaluation, decimal Unreceived);

/// <summary>Trạng thái trước một lần nhận, để hoàn lại khi lưu thất bại.</summary>
public sealed record ReceiptUndo(
    PurchaseStatus Status,
    DateTimeOffset? ReceivedAt,
    string ReceivedBy,
    DateTimeOffset? UpdatedAt,
    string UpdatedBy,
    IReadOnlyList<(Guid LineId, int Received, decimal Costed)> Lines,
    Guid EventId);

public sealed partial class PurchaseOrder
{
    public const int ReasonMaxLength = 500;

    private readonly List<PurchaseEvent> _events = [];

    private readonly List<PurchaseCostAdjustment> _adjustments = [];

    public IReadOnlyList<PurchaseEvent> Events => _events;

    public IReadOnlyList<PurchaseCostAdjustment> Adjustments => _adjustments;

    /// <summary>Còn sửa được thông tin, mặt hàng, chi phí: trước lần nhận hàng đầu tiên và chưa hủy.</summary>
    public bool IsEditable =>
        Status is PurchaseStatus.Draft or PurchaseStatus.PendingApproval or PurchaseStatus.Approved or PurchaseStatus.InTransit;

    /// <summary>Chỉ đơn đã duyệt (hoặc đang về, đã nhận một phần) mới nhận hàng được.</summary>
    public bool IsReceivable =>
        Status is PurchaseStatus.Approved or PurchaseStatus.InTransit or PurchaseStatus.PartiallyReceived;

    /// <summary>Đơn còn dang dở: chưa nhận đủ và chưa hủy.</summary>
    public bool IsOutstanding => Status is not (PurchaseStatus.Received or PurchaseStatus.Cancelled);

    public bool TrySubmit(DateTimeOffset? at = null, string? by = null) =>
        Move(PurchaseStatus.Draft, PurchaseStatus.PendingApproval, PurchaseEventKind.Submitted, at, by, null);

    public bool TryApprove(DateTimeOffset? at = null, string? by = null) =>
        Move(PurchaseStatus.PendingApproval, PurchaseStatus.Approved, PurchaseEventKind.Approved, at, by, null);

    public bool TryReturnToDraft(string? note, DateTimeOffset? at = null, string? by = null) =>
        Move(PurchaseStatus.PendingApproval, PurchaseStatus.Draft, PurchaseEventKind.ReturnedToDraft, at, by, note);

    public bool TryMarkInTransit(DateTimeOffset? at = null, string? by = null) =>
        Move(PurchaseStatus.Approved, PurchaseStatus.InTransit, PurchaseEventKind.InTransit, at, by, null);

    /// <summary>Hủy đơn chưa nhận món nào. Bắt buộc có lý do.</summary>
    public bool TryCancel(string? reason, DateTimeOffset? at = null, string? by = null)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (!IsEditable || trimmed.Length is 0 or > ReasonMaxLength)
        {
            return false;
        }

        Status = PurchaseStatus.Cancelled;
        Record(PurchaseEventKind.Cancelled, at, by, trimmed);
        return true;
    }

    /// <summary>
    /// Ghi một lần nhận hàng theo số thực nhận từng dòng (dòng không có trong <paramref name="actualByLine"/> coi là 0).
    /// Chưa phải lần cuối: mỗi món vào kho theo giá vốn dự toán (giá vốn dòng ÷ số đặt) và không được nhận lố số đặt.
    /// Lần cuối (<paramref name="final"/>): phần giá vốn còn lại của dòng dồn hết vào số nhận lần này, nên tổng giá vốn
    /// vào kho bằng đúng giá vốn dòng dù nhận thiếu hay thừa.
    /// </summary>
    public ReceiptDecision TryRecordReceipt(
        IReadOnlyDictionary<Guid, int> actualByLine,
        bool final,
        string? note,
        DateTimeOffset at,
        string? by,
        out IReadOnlyList<ReceiptLineResult> results,
        out ReceiptUndo? undo)
    {
        results = [];
        undo = null;
        if (!IsReceivable)
        {
            return ReceiptDecision.WrongStatus;
        }

        var quantities = new Dictionary<Guid, int>();
        foreach (var line in _lines)
        {
            var quantity = actualByLine.GetValueOrDefault(line.Id);
            if (quantity < 0 || (long)line.ReceivedQuantity + quantity > int.MaxValue || (!final && line.ReceivedQuantity + quantity > line.Quantity))
            {
                return ReceiptDecision.InvalidQuantity;
            }

            quantities[line.Id] = quantity;
        }

        var receivedNow = quantities.Values.Sum(value => (long)value);
        var receivedBefore = _lines.Sum(line => (long)line.ReceivedQuantity);
        if (actualByLine.Keys.Any(id => !quantities.ContainsKey(id)) || receivedNow + receivedBefore == 0 || (!final && receivedNow == 0))
        {
            return ReceiptDecision.InvalidQuantity;
        }

        var trimmedNote = note?.Trim() ?? string.Empty;
        if (final && trimmedNote.Length == 0 && _lines.Any(line => line.ReceivedQuantity + quantities[line.Id] != line.Quantity))
        {
            return ReceiptDecision.NoteRequired;
        }

        var built = new List<ReceiptLineResult>(_lines.Count);
        var previous = new List<(Guid LineId, int Received, decimal Costed)>(_lines.Count);
        foreach (var line in _lines)
        {
            previous.Add((line.Id, line.ReceivedQuantity, line.CostedAmount));
            var quantity = quantities[line.Id];
            var landed = PurchaseMoney.RoundDong(PurchaseCosting.For(this, line).LandedCost);
            decimal unit = 0;
            decimal revaluation = 0;
            decimal unreceived = 0;
            var costed = line.CostedAmount;
            if (!final)
            {
                unit = PurchaseMoney.RoundDong(landed / line.Quantity);
                costed += unit * quantity;
            }
            else if (quantity > 0)
            {
                unit = PurchaseMoney.RoundDong(Math.Max(0, landed - line.CostedAmount) / quantity);
                costed += unit * quantity;
            }
            else if (line.ReceivedQuantity > 0)
            {
                revaluation = landed - line.CostedAmount;
                costed = landed;
            }
            else
            {
                unreceived = landed;
            }

            line.SetReceipt(line.ReceivedQuantity + quantity, costed);
            built.Add(new ReceiptLineResult(line, quantity, unit, revaluation, unreceived));
        }

        var before = (Status, ReceivedAt, ReceivedBy, UpdatedAt, UpdatedBy);
        Status = final ? PurchaseStatus.Received : PurchaseStatus.PartiallyReceived;
        ReceivedAt = at;
        ReceivedBy = FitActor(by);
        StampUpdated(at, by);
        var recorded = Record(final ? PurchaseEventKind.Received : PurchaseEventKind.PartiallyReceived, at, by, trimmedNote);
        results = built;
        undo = new ReceiptUndo(before.Status, before.ReceivedAt, before.ReceivedBy, before.UpdatedAt, before.UpdatedBy, previous, recorded.Id);
        return ReceiptDecision.Ok;
    }

    /// <summary>Hoàn lại một lần nhận chưa lưu được.</summary>
    public void UndoReceipt(ReceiptUndo undo)
    {
        Status = undo.Status;
        ReceivedAt = undo.ReceivedAt;
        ReceivedBy = undo.ReceivedBy;
        UpdatedAt = undo.UpdatedAt;
        UpdatedBy = undo.UpdatedBy;
        foreach (var (lineId, received, costed) in undo.Lines)
        {
            _lines.FirstOrDefault(line => line.Id == lineId)?.SetReceipt(received, costed);
        }

        _events.RemoveAll(item => item.Id == undo.EventId);
    }

    /// <summary>Ghi một khoản điều chỉnh chi phí sau nhập. Chỉ khi đơn đã nhận đủ; số tiền khác 0; bắt buộc có tên khoản và lý do.</summary>
    public PurchaseCostAdjustment? TryAddCostAdjustment(
        string? label,
        decimal amount,
        ChargeSplitMethod method,
        string? reason,
        decimal appliedToStock,
        DateTimeOffset at,
        string? by)
    {
        var name = label?.Trim() ?? string.Empty;
        var why = reason?.Trim() ?? string.Empty;
        var rounded = PurchaseMoney.RoundDong(amount);
        var applied = PurchaseMoney.RoundDong(appliedToStock);
        if (Status != PurchaseStatus.Received
            || rounded == 0
            || name.Length is 0 or > PurchaseCharge.LabelMaxLength
            || why.Length is 0 or > PurchaseCostAdjustment.ReasonMaxLength
            || method == ChargeSplitMethod.Manual
            || !Enum.IsDefined(method)
            || Math.Abs(applied) > Math.Abs(rounded)
            || (applied != 0 && Math.Sign(applied) != Math.Sign(rounded)))
        {
            return null;
        }

        var adjustment = PurchaseCostAdjustment.Create(Id, name, rounded, method, why, applied, at, FitActor(by));
        _adjustments.Add(adjustment);
        StampUpdated(at, by);
        Record(PurchaseEventKind.CostAdjusted, at, by, name);
        return adjustment;
    }

    private bool Move(PurchaseStatus from, PurchaseStatus to, PurchaseEventKind kind, DateTimeOffset? at, string? by, string? note)
    {
        if (Status != from)
        {
            return false;
        }

        Status = to;
        Record(kind, at, by, note);
        return true;
    }

    private PurchaseEvent Record(PurchaseEventKind kind, DateTimeOffset? at, string? by, string? note)
    {
        var recorded = PurchaseEvent.Create(Id, kind, at ?? DateTimeOffset.UtcNow, FitActor(by), note);
        _events.Add(recorded);
        return recorded;
    }
}
