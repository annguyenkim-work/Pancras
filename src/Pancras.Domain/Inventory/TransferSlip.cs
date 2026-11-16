namespace Pancras.Domain.Inventory;

public enum TransferStatus
{
    /// <summary>Không còn dùng (giữ để đọc dữ liệu cũ).</summary>
    Draft = 1,
    InTransit = 2,
    Received = 3,
    Cancelled = 4,
    /// <summary>Phiếu đề nghị chờ duyệt, chưa ảnh hưởng tồn kho.</summary>
    Requested = 5,
    Approved = 6,
    Rejected = 7,
    /// <summary>Đã nhập kho theo số thực nhận nhưng khác số xuất; chờ Kế toán/Giám đốc xử lý chênh lệch.</summary>
    ReceivedWithDiscrepancy = 8,
    /// <summary>Hàng đang vận chuyển nhưng được trả về kho xuất (giao nhầm, kho nhận từ chối...). Tồn đã hoàn lại.</summary>
    Returned = 9
}

public sealed class TransferLine
{
    private TransferLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid TransferSlipId { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Số kho đích thực nhận; null khi chưa nhận.</summary>
    public int? ReceivedQuantity { get; private set; }

    internal void RecordReceived(int quantity) => ReceivedQuantity = quantity;

    internal void ClearReceived() => ReceivedQuantity = null;

    internal static TransferLine Create(Guid transferSlipId, Guid catalogItemId, int quantity) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TransferSlipId = transferSlipId,
            CatalogItemId = catalogItemId,
            Quantity = quantity
        };
}

public sealed class TransferSlip : Pancras.Domain.Auditing.VersionedAuditStamped
{
    public const int NoteMaxLength = 500;

    public const int SealMaxLength = 100;

    public const int ReasonMaxLength = 500;

    public const int CarrierMaxLength = 200;

    private readonly List<TransferLine> _lines = [];

    private TransferSlip()
    {
        Note = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SourceLocationId { get; private set; }

    public Guid DestinationLocationId { get; private set; }

    /// <summary>Nhân viên lập phiếu; chỉ người này được sửa hoặc hủy phiếu. Rỗng với phiếu cũ.</summary>
    public Guid RequestedById { get; private set; }

    /// <summary>Mã duy nhất do form lập phiếu gửi kèm, để bấm đúp hoặc gửi lại không tạo phiếu thứ hai.</summary>
    public Guid? RequestKey { get; private set; }

    /// <summary>Phiếu đã duyệt đang giữ hàng ở kho xuất (tăng số giữ chỗ). Phiếu duyệt trước khi có tính năng giữ hàng thì false.</summary>
    public bool StockReserved { get; private set; }

    public string Note { get; private set; }

    public string SealNumber { get; private set; } = string.Empty;

    /// <summary>Lý do chênh lệch do bên nhận ghi khi số thực nhận khác số xuất.</summary>
    public string ReceiveNote { get; private set; } = string.Empty;

    /// <summary>Cách xử lý chênh lệch do Kế toán/Giám đốc ghi, hoặc lý do từ chối phiếu đề nghị.</summary>
    public string ResolutionNote { get; private set; } = string.Empty;

    /// <summary>Ghi chú tự do về người vận chuyển do người xuất kho nhập; hệ thống không quản lý tài xế.</summary>
    public string Carrier { get; private set; } = string.Empty;

    public string ApprovedBy { get; private set; } = string.Empty;

    public DateTimeOffset? ApprovedAt { get; private set; }

    public string ShippedBy { get; private set; } = string.Empty;

    public DateTimeOffset? ShippedAt { get; private set; }

    public string ReceivedBy { get; private set; } = string.Empty;

    public DateTimeOffset? ReceivedAt { get; private set; }

    public string ResolvedBy { get; private set; } = string.Empty;

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Ghi ai (mã nhân viên) làm bước nào lúc nào.</summary>
    public void RecordStep(TransferStep step, DateTimeOffset at, string? by)
    {
        var actor = FitActor(by);
        switch (step)
        {
            case TransferStep.Approved:
                ApprovedBy = actor;
                ApprovedAt = at;
                break;
            case TransferStep.Shipped:
                ShippedBy = actor;
                ShippedAt = at;
                break;
            case TransferStep.Received:
                ReceivedBy = actor;
                ReceivedAt = at;
                break;
            case TransferStep.Resolved:
                ResolvedBy = actor;
                ResolvedAt = at;
                break;
        }
    }

    public bool HasDiscrepancy => Lines.Any(line => line.ReceivedQuantity is int received && received != line.Quantity);

    public TransferStatus Status { get; private set; }

    public IReadOnlyList<TransferLine> Lines => _lines;

    public static TransferSlip? Create(
        Guid sourceLocationId,
        Guid destinationLocationId,
        string? note,
        IReadOnlyList<(Guid CatalogItemId, int Quantity)> lines,
        Guid requestedById = default,
        Guid? requestKey = null)
    {
        if (sourceLocationId == Guid.Empty
            || destinationLocationId == Guid.Empty
            || sourceLocationId == destinationLocationId)
        {
            return null;
        }

        if (!InventoryText.TryOptional(note, NoteMaxLength, out var storedNote))
        {
            return null;
        }

        if (lines is null || lines.Count == 0 || lines.Any(line => line.CatalogItemId == Guid.Empty || line.Quantity <= 0))
        {
            return null;
        }

        var slip = new TransferSlip
        {
            Id = Guid.CreateVersion7(),
            SourceLocationId = sourceLocationId,
            DestinationLocationId = destinationLocationId,
            Note = storedNote,
            RequestedById = requestedById,
            RequestKey = requestKey,
            Status = TransferStatus.Requested
        };
        foreach (var line in lines)
        {
            slip._lines.Add(TransferLine.Create(slip.Id, line.CatalogItemId, line.Quantity));
        }

        return slip;
    }

    /// <summary>Sửa phiếu đề nghị khi chưa được duyệt: kho, ghi chú và danh sách hàng. Kiểm tra giống lúc tạo.</summary>
    public bool TryUpdateRequest(
        Guid sourceLocationId,
        Guid destinationLocationId,
        string? note,
        IReadOnlyList<(Guid CatalogItemId, int Quantity)> lines)
    {
        if (Status != TransferStatus.Requested)
        {
            return false;
        }

        var replacement = Create(sourceLocationId, destinationLocationId, note, lines);
        if (replacement is null)
        {
            return false;
        }

        SourceLocationId = replacement.SourceLocationId;
        DestinationLocationId = replacement.DestinationLocationId;
        Note = replacement.Note;
        _lines.Clear();
        foreach (var line in replacement._lines)
        {
            _lines.Add(TransferLine.Create(Id, line.CatalogItemId, line.Quantity));
        }

        return true;
    }

    public bool TryApprove(IReadOnlyDictionary<Guid, StockBalance> sources)
    {
        if (Status != TransferStatus.Requested)
        {
            return false;
        }

        foreach (var (itemId, quantity) in Totals())
        {
            if (!sources.TryGetValue(itemId, out var source) || !source.CanShip(quantity))
            {
                return false;
            }
        }

        foreach (var (itemId, quantity) in Totals())
        {
            if (!sources[itemId].TryReserve(quantity))
            {
                throw new InvalidOperationException("Reservation precheck did not hold.");
            }
        }

        StockReserved = true;
        Status = TransferStatus.Approved;
        return true;
    }

    public bool TryReject(string? reason)
    {
        if (Status != TransferStatus.Requested || !InventoryText.TryOptional(reason, ReasonMaxLength, out var stored))
        {
            return false;
        }

        ResolutionNote = stored;
        Status = TransferStatus.Rejected;
        return true;
    }

    /// <summary>Hủy phiếu chưa xuất kho. Phiếu đã duyệt đang giữ hàng thì nhả phần giữ ở kho xuất; tồn thực không đổi.</summary>
    public bool TryCancelBeforeShipment(IReadOnlyDictionary<Guid, StockBalance>? sources = null)
    {
        if (Status is not (TransferStatus.Requested or TransferStatus.Approved))
        {
            return false;
        }

        if (StockReserved)
        {
            var totals = Totals();
            if (sources is null || totals.Any(pair => !sources.TryGetValue(pair.Key, out var source) || source.Reserved < pair.Value))
            {
                return false;
            }

            foreach (var (itemId, quantity) in totals)
            {
                if (!sources[itemId].TryRelease(quantity))
                {
                    throw new InvalidOperationException("Release precheck did not hold.");
                }
            }

            StockReserved = false;
        }

        Status = TransferStatus.Cancelled;
        return true;
    }

    /// <summary>
    /// Trả hàng đang vận chuyển về kho xuất: bỏ phần đang chuyển ở kho đích và cộng lại tồn kho xuất.
    /// Chỉ làm khi phiếu còn InTransit; cần lý do.
    /// </summary>
    public bool TryReturnToSource(
        IReadOnlyDictionary<Guid, StockBalance> sources,
        IReadOnlyDictionary<Guid, StockBalance> destinations,
        string? reason)
    {
        if (Status != TransferStatus.InTransit
            || !InventoryText.TryRequired(reason, ReasonMaxLength, out var stored))
        {
            return false;
        }

        foreach (var (itemId, quantity) in Totals())
        {
            if (!sources.TryGetValue(itemId, out var source)
                || !destinations.TryGetValue(itemId, out var destination)
                || destination.InTransit < quantity
                || (long)source.OnHand + quantity > int.MaxValue)
            {
                return false;
            }
        }

        foreach (var (itemId, quantity) in Totals())
        {
            if (!destinations[itemId].TryRemoveInTransit(quantity) || !sources[itemId].TryAddOnHand(quantity))
            {
                throw new InvalidOperationException("Return precheck did not hold.");
            }
        }

        ResolutionNote = stored;
        Status = TransferStatus.Returned;
        return true;
    }

    public bool TryConfirmShipment(
        IReadOnlyDictionary<Guid, StockBalance> sources,
        IReadOnlyDictionary<Guid, StockBalance> destinations,
        string? sealNumber = null,
        string? carrier = null)
    {
        if (Status != TransferStatus.Approved
            || !InventoryText.TryOptional(sealNumber, SealMaxLength, out var seal)
            || !InventoryText.TryOptional(carrier, CarrierMaxLength, out var carrierNote)
            || !TotalsAreShippable(sources, destinations))
        {
            return false;
        }

        SealNumber = seal;
        Carrier = carrierNote;

        foreach (var (itemId, quantity) in Totals())
        {
            var shipped = StockReserved ? sources[itemId].TryShipReserved(quantity) : sources[itemId].TryShip(quantity);
            if (!shipped || !destinations[itemId].TryAddInTransit(quantity))
            {
                throw new InvalidOperationException("Shipment precheck did not hold.");
            }
        }

        StockReserved = false;
        Status = TransferStatus.InTransit;
        return true;
    }

    /// <summary>
    /// Nhận hàng theo số thực nhận từng dòng (null = nhận đủ). Khác số xuất thì phiếu chuyển sang chờ xử lý chênh lệch và cần lý do.
    /// Phần đang chuyển ở kho đích luôn giảm đúng số đã xuất; tồn thực tế tăng đúng số thực nhận.
    /// </summary>
    public bool TryConfirmReceipt(
        IReadOnlyDictionary<Guid, StockBalance> destinations,
        IReadOnlyDictionary<Guid, int>? actualByLine = null,
        string? discrepancyReason = null)
    {
        if (Status != TransferStatus.InTransit)
        {
            return false;
        }

        var actuals = _lines.ToDictionary(line => line.Id, line => actualByLine is not null && actualByLine.TryGetValue(line.Id, out var value) ? value : line.Quantity);
        if (actuals.Values.Any(value => value < 0)
            || (actualByLine is not null && actualByLine.Keys.Any(id => !actuals.ContainsKey(id))))
        {
            return false;
        }

        var differs = _lines.Any(line => actuals[line.Id] != line.Quantity);
        if (!InventoryText.TryOptional(discrepancyReason, ReasonMaxLength, out var reason) || (differs && reason.Length == 0))
        {
            return false;
        }

        foreach (var (itemId, quantity) in Totals())
        {
            var actual = _lines.Where(line => line.CatalogItemId == itemId).Sum(line => (long)actuals[line.Id]);
            if (!destinations.TryGetValue(itemId, out var destination)
                || destination.InTransit < quantity
                || actual > int.MaxValue
                || (long)destination.OnHand + actual > int.MaxValue)
            {
                return false;
            }
        }

        foreach (var (itemId, quantity) in Totals())
        {
            var actual = (int)_lines.Where(line => line.CatalogItemId == itemId).Sum(line => (long)actuals[line.Id]);
            if (!destinations[itemId].TryReceiveActual(quantity, actual))
            {
                throw new InvalidOperationException("Receipt precheck did not hold.");
            }
        }

        foreach (var line in _lines)
        {
            line.RecordReceived(actuals[line.Id]);
        }

        ReceiveNote = reason;
        Status = differs ? TransferStatus.ReceivedWithDiscrepancy : TransferStatus.Received;
        return true;
    }

    /// <summary>Kế toán/Giám đốc xác nhận cách xử lý chênh lệch; phiếu đóng.</summary>
    public bool TryResolveDiscrepancy(string? resolution)
    {
        if (Status != TransferStatus.ReceivedWithDiscrepancy
            || !InventoryText.TryOptional(resolution, ReasonMaxLength, out var stored)
            || stored.Length == 0)
        {
            return false;
        }

        ResolutionNote = stored;
        Status = TransferStatus.Received;
        return true;
    }

    /// <summary>Khôi phục cờ giữ hàng khi lưu thất bại (số giữ chỗ ở kho xuất được khôi phục riêng bằng BalanceSnapshot).</summary>
    public void RestoreReserved(bool reserved) => StockReserved = reserved;

    public void RestoreUncommitted(TransferStatus status)
    {
        Status = status;
        foreach (var line in _lines)
        {
            line.ClearReceived();
        }

        ReceiveNote = string.Empty;
        ResolutionNote = string.Empty;
        ResolvedBy = string.Empty;
        ResolvedAt = null;
        ReceivedBy = string.Empty;
        ReceivedAt = null;
        if (status is TransferStatus.Requested or TransferStatus.Approved)
        {
            SealNumber = string.Empty;
            Carrier = string.Empty;
            ShippedBy = string.Empty;
            ShippedAt = null;
        }

        if (status == TransferStatus.Requested)
        {
            StockReserved = false;
            ApprovedBy = string.Empty;
            ApprovedAt = null;
        }
    }

    private bool TotalsAreShippable(
        IReadOnlyDictionary<Guid, StockBalance> sources,
        IReadOnlyDictionary<Guid, StockBalance> destinations)
    {
        foreach (var (itemId, quantity) in Totals())
        {
            if (!sources.TryGetValue(itemId, out var source)
                || !(StockReserved ? source.Reserved >= quantity && source.OnHand >= quantity : source.CanShip(quantity)))
            {
                return false;
            }

            if (!destinations.ContainsKey(itemId))
            {
                return false;
            }
        }

        return Totals().Count > 0;
    }

    private Dictionary<Guid, int> Totals()
    {
        var totals = new Dictionary<Guid, int>();
        foreach (var line in _lines)
        {
            totals[line.CatalogItemId] = totals.GetValueOrDefault(line.CatalogItemId) + line.Quantity;
        }

        return totals;
    }
}

public enum TransferStep
{
    Approved = 1,
    Shipped = 2,
    Received = 3,
    Resolved = 4
}
