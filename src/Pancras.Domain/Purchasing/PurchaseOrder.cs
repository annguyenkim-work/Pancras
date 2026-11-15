namespace Pancras.Domain.Purchasing;

public enum PurchaseStatus
{
    /// <summary>Nháp: đang soạn (trước đây gọi là "Đang mở").</summary>
    Draft = 1,
    Received = 2,
    PendingApproval = 3,
    Approved = 4,
    InTransit = 5,
    Cancelled = 6,

    /// <summary>Đã nhận một phần, còn hàng về sau.</summary>
    PartiallyReceived = 7
}

public enum ChargeSplitMethod
{
    FobValue = 1,
    Quantity = 2,
    Weight = 3,
    Volume = 4,
    Manual = 5
}

public enum ChargeGroup
{
    CustomsValueAdjustment = 1,
    Logistics = 2,
    DeductibleTax = 3,
    NonDeductibleTax = 4
}

public sealed class PurchaseLine
{
    private PurchaseLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid PurchaseOrderId { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public int Quantity { get; private set; }

    public decimal FobUsd { get; private set; }

    public decimal DutyRate { get; private set; }

    public decimal SupplementalDuty { get; private set; }

    public decimal SpecialConsumptionTax { get; private set; }

    public decimal EnvironmentTax { get; private set; }

    /// <summary>Số lượng đã nhận thực tế, cộng dồn qua các lần nhận.</summary>
    public int ReceivedQuantity { get; private set; }

    /// <summary>Giá vốn (đồng) đã đưa vào tồn kho cho dòng này, cộng dồn qua các lần nhận.</summary>
    public decimal CostedAmount { get; private set; }

    /// <summary>Thứ tự nhập trên đơn. Phần lẻ khi chia chi phí dồn về dòng có số này lớn nhất.</summary>
    public int SortOrder { get; private set; }

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    internal void SetReceipt(int receivedQuantity, decimal costedAmount)
    {
        ReceivedQuantity = receivedQuantity;
        CostedAmount = costedAmount;
    }

    internal static PurchaseLine? Create(
        Guid orderId,
        Guid catalogItemId,
        int quantity,
        decimal fobUsd,
        decimal dutyRate,
        decimal supplementalDuty,
        decimal specialConsumptionTax,
        decimal environmentTax)
    {
        if (catalogItemId == Guid.Empty || quantity <= 0 || fobUsd < 0 || dutyRate < 0
            || supplementalDuty < 0 || specialConsumptionTax < 0 || environmentTax < 0)
        {
            return null;
        }

        return new PurchaseLine
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = orderId,
            CatalogItemId = catalogItemId,
            Quantity = quantity,
            FobUsd = PurchaseMoney.RoundUsd(fobUsd),
            DutyRate = PurchaseMoney.RoundRate(dutyRate),
            SupplementalDuty = PurchaseMoney.RoundDong(supplementalDuty),
            SpecialConsumptionTax = PurchaseMoney.RoundDong(specialConsumptionTax),
            EnvironmentTax = PurchaseMoney.RoundDong(environmentTax)
        };
    }

    internal void Update(
        Guid catalogItemId,
        int quantity,
        decimal fobUsd,
        decimal dutyRate,
        decimal supplementalDuty,
        decimal specialConsumptionTax,
        decimal environmentTax)
    {
        CatalogItemId = catalogItemId;
        Quantity = quantity;
        FobUsd = PurchaseMoney.RoundUsd(fobUsd);
        DutyRate = PurchaseMoney.RoundRate(dutyRate);
        SupplementalDuty = PurchaseMoney.RoundDong(supplementalDuty);
        SpecialConsumptionTax = PurchaseMoney.RoundDong(specialConsumptionTax);
        EnvironmentTax = PurchaseMoney.RoundDong(environmentTax);
    }
}

public sealed class PurchaseChargeShare
{
    private PurchaseChargeShare()
    {
    }

    public Guid Id { get; private set; }

    public Guid PurchaseChargeId { get; private set; }

    public Guid LineId { get; private set; }

    public decimal Amount { get; private set; }

    internal static PurchaseChargeShare Create(Guid chargeId, Guid lineId, decimal amount) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            PurchaseChargeId = chargeId,
            LineId = lineId,
            Amount = amount
        };
}

public sealed class PurchaseCharge
{
    private readonly List<PurchaseChargeShare> _shares = [];

    private PurchaseCharge()
    {
    }

    public Guid Id { get; private set; }

    public Guid PurchaseOrderId { get; private set; }

    public decimal Amount { get; private set; }

    public ChargeSplitMethod Method { get; private set; }

    public ChargeGroup? Group { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public const int LabelMaxLength = 200;

    public IReadOnlyList<PurchaseChargeShare> Shares => _shares;

    internal static PurchaseCharge? Create(Guid orderId, decimal amount, ChargeSplitMethod method, string? label = null)
    {
        var trimmed = label?.Trim() ?? string.Empty;
        if (amount < 0 || !Enum.IsDefined(method) || trimmed.Length > LabelMaxLength)
        {
            return null;
        }

        return new PurchaseCharge
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = orderId,
            Amount = PurchaseMoney.RoundDong(amount),
            Method = method,
            Label = trimmed
        };
    }

    internal void ReplaceShares(IReadOnlyList<(Guid LineId, decimal Amount)> shares)
    {
        _shares.Clear();
        foreach (var share in shares)
        {
            _shares.Add(PurchaseChargeShare.Create(Id, share.LineId, share.Amount));
        }
    }

    internal void SetGroup(ChargeGroup group) => Group = group;

    internal bool TryUpdate(decimal amount, ChargeSplitMethod method, string? label)
    {
        var trimmed = label?.Trim() ?? string.Empty;
        if (amount < 0 || !Enum.IsDefined(method) || trimmed.Length > LabelMaxLength)
        {
            return false;
        }

        Amount = PurchaseMoney.RoundDong(amount);
        Method = method;
        Label = trimmed;
        return true;
    }

    internal void RemoveSharesOf(IReadOnlySet<Guid> lineIds) => _shares.RemoveAll(share => lineIds.Contains(share.LineId));
}

public sealed partial class PurchaseOrder : Pancras.Domain.Auditing.AuditStamped
{
    public const int CodeMaxLength = 32;

    public const int ContainerMaxLength = 64;

    public const int PortMaxLength = 100;

    private readonly List<PurchaseLine> _lines = [];

    private readonly List<PurchaseCharge> _charges = [];

    private PurchaseOrder()
    {
        Code = string.Empty;
        NormalizedCode = string.Empty;
        ContainerNumber = string.Empty;
        BillOfLading = string.Empty;
        Port = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string NormalizedCode { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>Mã nhà cung cấp tại thời điểm lập đơn. Giữ nguyên dù hồ sơ nhà cung cấp đổi mã sau đó.</summary>
    public string SupplierCode { get; private set; } = string.Empty;

    /// <summary>Tên nhà cung cấp tại thời điểm lập đơn. Chứng từ phải giữ đúng tên lúc đặt hàng.</summary>
    public string SupplierName { get; private set; } = string.Empty;

    /// <summary>Chụp lại mã và tên nhà cung cấp lên đơn (lúc lập đơn, hoặc khi đổi sang nhà cung cấp khác).</summary>
    public void RecordSupplier(string code, string name)
    {
        SupplierCode = code.Trim();
        SupplierName = name.Trim();
    }

    public Guid ReceiveLocationId { get; private set; }

    public string ContainerNumber { get; private set; }

    public string BillOfLading { get; private set; }

    public string Port { get; private set; }

    public DateOnly? ExpectedOn { get; private set; }

    public decimal ExchangeRate { get; private set; }

    /// <summary>Ngày của tỷ giá (thường là ngày đăng ký tờ khai).</summary>
    public DateOnly? ExchangeRateOn { get; private set; }

    /// <summary>Nguồn tỷ giá (ví dụ: tờ khai hải quan, ngân hàng).</summary>
    public string ExchangeRateSource { get; private set; } = string.Empty;

    public const int RateSourceMaxLength = 100;

    public decimal VatRate { get; private set; }

    public PurchaseStatus Status { get; private set; }

    /// <summary>Tăng mỗi lần lưu để form đang mở bị từ chối nếu đơn đã được sửa.</summary>
    public int Version { get; private set; }

    public void AdvanceVersion() => Version++;

    /// <summary>VAT đầu vào được khấu trừ thì không vào giá vốn; không được khấu trừ thì cộng vào giá vốn.</summary>
    public bool VatDeductible { get; private set; } = true;

    // UpdatedAt/By kế thừa từ AuditStamped. Đơn mua tự đóng dấu bằng handler (thời điểm của đồng hồ ứng dụng), kể cả lúc tạo.
    // để nhập kho ghi được cả ReceivedAt; các thực thể khác do tầng lưu trữ đóng dấu tự động.

    public DateTimeOffset? ReceivedAt { get; private set; }

    public string ReceivedBy { get; private set; } = string.Empty;

    public IReadOnlyList<PurchaseLine> Lines =>
        _lines.OrderBy(line => line.SortOrder).ThenBy(line => line.Id).ToArray();

    public IReadOnlyList<PurchaseCharge> Charges => _charges;

    public bool HasUngroupedCharges => _charges.Any(charge => charge.Group is null);

    public static PurchaseOrder? TryCreate(
        string? code,
        Guid supplierId,
        Guid receiveLocationId,
        string? containerNumber,
        string? billOfLading,
        string? port,
        DateOnly? expectedOn,
        decimal exchangeRate,
        decimal vatRate,
        IReadOnlyList<(Guid CatalogItemId, int Quantity, decimal FobUsd, decimal DutyRate, decimal SupplementalDuty, decimal SpecialConsumptionTax, decimal EnvironmentTax)> lines,
        bool vatDeductible = true,
        DateTimeOffset? createdAt = null,
        string? createdBy = null,
        DateOnly? exchangeRateOn = null,
        string? exchangeRateSource = null)
    {
        var trimmedCode = code?.Trim() ?? string.Empty;
        if (trimmedCode.Length is < 1 or > CodeMaxLength || supplierId == Guid.Empty || receiveLocationId == Guid.Empty)
        {
            return null;
        }

        if (!TryOptional(containerNumber, ContainerMaxLength, out var container)
            || !TryOptional(billOfLading, ContainerMaxLength, out var bill)
            || !TryOptional(port, PortMaxLength, out var storedPort))
        {
            return null;
        }

        if (exchangeRate <= 0 || vatRate < 0 || lines is null || lines.Count == 0)
        {
            return null;
        }

        var roundedRate = PurchaseMoney.RoundDong(exchangeRate);
        if (roundedRate <= 0)
        {
            return null;
        }

        var order = new PurchaseOrder
        {
            Id = Guid.CreateVersion7(),
            Code = trimmedCode,
            NormalizedCode = trimmedCode.ToUpperInvariant(),
            SupplierId = supplierId,
            ReceiveLocationId = receiveLocationId,
            ContainerNumber = container,
            BillOfLading = bill,
            Port = storedPort,
            ExpectedOn = expectedOn,
            ExchangeRate = roundedRate,
            VatRate = PurchaseMoney.RoundRate(vatRate),
            VatDeductible = vatDeductible,
            ExchangeRateOn = exchangeRateOn,
            ExchangeRateSource = FitSource(exchangeRateSource),
            Status = PurchaseStatus.Draft,
            UpdatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedBy = FitActor(createdBy)
        };
        if (order.NormalizedCode.Length is < 1 or > CodeMaxLength)
        {
            return null;
        }

        var sort = 0;
        foreach (var line in lines)
        {
            var created = PurchaseLine.Create(
                order.Id,
                line.CatalogItemId,
                line.Quantity,
                line.FobUsd,
                line.DutyRate,
                line.SupplementalDuty,
                line.SpecialConsumptionTax,
                line.EnvironmentTax);
            if (created is null)
            {
                return null;
            }

            created.SetSortOrder(sort);
            sort++;
            order._lines.Add(created);
        }

        order.Record(PurchaseEventKind.Created, order.UpdatedAt, createdBy, null);
        return order;
    }

    /// <summary>Sửa thông tin chung. Chỉ khi đơn chưa nhập kho.</summary>
    public bool TryUpdateHeader(
        string? code,
        Guid supplierId,
        Guid receiveLocationId,
        string? containerNumber,
        string? billOfLading,
        string? port,
        DateOnly? expectedOn,
        decimal exchangeRate,
        decimal vatRate,
        bool vatDeductible,
        DateOnly? exchangeRateOn = null,
        string? exchangeRateSource = null)
    {
        var trimmedCode = code?.Trim() ?? string.Empty;
        if (!IsEditable
            || trimmedCode.Length is < 1 or > CodeMaxLength
            || supplierId == Guid.Empty
            || receiveLocationId == Guid.Empty
            || exchangeRate <= 0
            || vatRate < 0
            || !TryOptional(containerNumber, ContainerMaxLength, out var container)
            || !TryOptional(billOfLading, ContainerMaxLength, out var bill)
            || !TryOptional(port, PortMaxLength, out var storedPort))
        {
            return false;
        }

        var roundedRate = PurchaseMoney.RoundDong(exchangeRate);
        if (roundedRate <= 0)
        {
            return false;
        }

        Code = trimmedCode;
        NormalizedCode = trimmedCode.ToUpperInvariant();
        SupplierId = supplierId;
        ReceiveLocationId = receiveLocationId;
        ContainerNumber = container;
        BillOfLading = bill;
        Port = storedPort;
        ExpectedOn = expectedOn;
        ExchangeRate = roundedRate;
        VatRate = PurchaseMoney.RoundRate(vatRate);
        VatDeductible = vatDeductible;
        ExchangeRateOn = exchangeRateOn;
        ExchangeRateSource = FitSource(exchangeRateSource);
        return true;
    }

    /// <summary>
    /// Thay danh sách dòng hàng. Dòng có <c>LineId</c> khớp giữ nguyên mã (và số tờ khai), dòng không còn thì bị bỏ,
    /// dòng mới được thêm. <paramref name="lineSetChanged"/> cho biết tập dòng có đổi để chia lại chi phí nhập tay.
    /// </summary>
    public bool TryUpdateLines(
        IReadOnlyList<(Guid? LineId, Guid CatalogItemId, int Quantity, decimal FobUsd, decimal DutyRate, decimal SupplementalDuty, decimal SpecialConsumptionTax, decimal EnvironmentTax)> lines,
        out bool lineSetChanged)
    {
        lineSetChanged = false;
        if (!IsEditable || lines is null || lines.Count == 0)
        {
            return false;
        }

        foreach (var line in lines)
        {
            if (PurchaseLine.Create(Id, line.CatalogItemId, line.Quantity, line.FobUsd, line.DutyRate, line.SupplementalDuty, line.SpecialConsumptionTax, line.EnvironmentTax) is null)
            {
                return false;
            }
        }

        var keptIds = new HashSet<Guid>();
        foreach (var line in lines)
        {
            if (line.LineId is Guid existingId && _lines.Any(item => item.Id == existingId) && !keptIds.Add(existingId))
            {
                return false;
            }
        }

        var removed = _lines.Where(item => !keptIds.Contains(item.Id)).Select(item => item.Id).ToHashSet();
        var added = false;
        var result = new List<PurchaseLine>(lines.Count);
        foreach (var line in lines)
        {
            var existing = line.LineId is Guid id ? _lines.FirstOrDefault(item => item.Id == id) : null;
            if (existing is not null)
            {
                existing.Update(line.CatalogItemId, line.Quantity, line.FobUsd, line.DutyRate, line.SupplementalDuty, line.SpecialConsumptionTax, line.EnvironmentTax);
                result.Add(existing);
                continue;
            }

            added = true;
            result.Add(PurchaseLine.Create(Id, line.CatalogItemId, line.Quantity, line.FobUsd, line.DutyRate, line.SupplementalDuty, line.SpecialConsumptionTax, line.EnvironmentTax)!);
        }

        lineSetChanged = added || removed.Count > 0;
        if (removed.Count > 0)
        {
            foreach (var charge in _charges)
            {
                charge.RemoveSharesOf(removed);
            }

            _lines.RemoveAll(item => removed.Contains(item.Id));
        }

        foreach (var line in result.Where(item => !_lines.Contains(item)))
        {
            _lines.Add(line);
        }

        for (var index = 0; index < result.Count; index++)
        {
            result[index].SetSortOrder(index);
        }

        _lines.Sort(static (left, right) => left.SortOrder.CompareTo(right.SortOrder));
        return true;
    }

    public bool TryAddCharge(decimal amount, ChargeSplitMethod method, out Guid chargeId, string? label = null)
    {
        chargeId = Guid.Empty;
        var charge = PurchaseCharge.Create(Id, amount, method, label);
        if (charge is null)
        {
            return false;
        }

        _charges.Add(charge);
        chargeId = charge.Id;
        return true;
    }

    public bool TryAllocate(Guid chargeId, IReadOnlyDictionary<Guid, decimal> basisByLine, bool manual)
    {
        var ordered = _lines.OrderBy(line => line.SortOrder).ThenBy(line => line.Id).ToArray();
        var charge = _charges.FirstOrDefault(item => item.Id == chargeId);
        if (charge is null || ordered.Length == 0)
        {
            return false;
        }

        if (manual)
        {
            if (basisByLine.Count != ordered.Length)
            {
                return false;
            }

            decimal sum = 0;
            var shares = new List<(Guid LineId, decimal Amount)>();
            foreach (var line in ordered)
            {
                if (!basisByLine.TryGetValue(line.Id, out var share) || share < 0)
                {
                    return false;
                }

                var rounded = PurchaseMoney.RoundDong(share);
                sum += rounded;
                shares.Add((line.Id, rounded));
            }

            if (sum != charge.Amount)
            {
                return false;
            }

            charge.ReplaceShares(shares);
            return true;
        }

        var weights = new List<decimal>(ordered.Length);
        foreach (var line in ordered)
        {
            if (!basisByLine.TryGetValue(line.Id, out var weight) || weight <= 0)
            {
                return false;
            }

            weights.Add(weight);
        }

        if (!ChargeAllocation.TrySplit(charge.Amount, weights, out var amounts))
        {
            return false;
        }

        var built = new List<(Guid LineId, decimal Amount)>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            built.Add((ordered[index].Id, amounts[index]));
        }

        charge.ReplaceShares(built);
        return true;
    }

    /// <summary>Sửa số tiền, cách chia, tên của một khoản (chưa nhập kho). Khoản chia tay không sửa qua đây; sau khi sửa phải chia lại bằng <see cref="TryAllocate"/>.</summary>
    public bool TryUpdateCharge(Guid chargeId, decimal amount, ChargeSplitMethod method, string? label)
    {
        var charge = _charges.FirstOrDefault(item => item.Id == chargeId);
        return IsEditable
            && charge is not null
            && charge.Method != ChargeSplitMethod.Manual
            && method != ChargeSplitMethod.Manual
            && charge.TryUpdate(amount, method, label);
    }

    public void DropCharge(Guid chargeId) => _charges.RemoveAll(charge => charge.Id == chargeId);

    public bool TryAssignGroup(Guid chargeId, ChargeGroup group, bool retainCurrent)
    {
        var charge = _charges.FirstOrDefault(item => item.Id == chargeId);
        if (charge is null || !Enum.IsDefined(group))
        {
            return false;
        }

        if (retainCurrent && charge.Group is ChargeGroup current && current != group)
        {
            return false;
        }

        charge.SetGroup(group);
        return true;
    }

    /// <summary>Ghi nhận một lần sửa đơn (thời điểm và mã nhân viên).</summary>
    public void StampUpdated(DateTimeOffset at, string? by)
    {
        UpdatedAt = at;
        UpdatedBy = FitActor(by);
    }

    private static string FitSource(string? source)
    {
        var trimmed = source?.Trim() ?? string.Empty;
        return trimmed.Length <= RateSourceMaxLength ? trimmed : trimmed[..RateSourceMaxLength];
    }

    private static bool TryOptional(string? value, int max, out string stored)
    {
        stored = value?.Trim() ?? string.Empty;
        return stored.Length <= max;
    }
}
