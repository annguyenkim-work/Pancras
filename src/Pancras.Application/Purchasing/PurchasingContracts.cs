using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed record PurchaseOrderSummary(
    Guid Id,
    string Code,
    int LineCount,
    string SupplierName,
    string ContainerNumber,
    string BillOfLading,
    string Port,
    DateOnly? ExpectedOn,
    string ReceiveLocationName,
    PurchaseStatus Status,
    decimal LandedTotal,
    decimal ExchangeRate = 0,
    bool CostVisible = true,
    Guid SupplierId = default);

public sealed record PurchaseLineView(
    Guid Id,
    Guid CatalogItemId,
    string ItemCode,
    string ItemName,
    int Quantity,
    decimal FobUsd,
    decimal DutyRate,
    decimal SupplementalDuty,
    decimal SpecialConsumptionTax,
    decimal EnvironmentTax,
    PurchaseLineCost Cost,
    int ReceivedQuantity = 0);

public sealed record PurchaseChargeView(
    Guid Id,
    decimal Amount,
    ChargeSplitMethod Method,
    ChargeGroup? Group,
    IReadOnlyList<PurchaseShareView> Shares,
    string Label = "");

public sealed record PurchaseShareView(Guid LineId, decimal Amount);

public sealed record PurchaseOrderDetail(
    Guid Id,
    string Code,
    string SupplierName,
    string ContainerNumber,
    string BillOfLading,
    string Port,
    DateOnly? ExpectedOn,
    string ReceiveLocationName,
    Guid ReceiveLocationId,
    decimal ExchangeRate,
    decimal VatRate,
    PurchaseStatus Status,
    IReadOnlyList<PurchaseLineView> Lines,
    IReadOnlyList<PurchaseChargeView> Charges,
    bool VatDeductible = true,
    Guid SupplierId = default,
    DateTimeOffset UpdatedAt = default,
    string UpdatedBy = "",
    DateTimeOffset? ReceivedAt = null,
    string ReceivedBy = "",
    DateOnly? ExchangeRateOn = null,
    string ExchangeRateSource = "",
    bool CostVisible = true,
    IReadOnlyList<PurchaseEventView>? Events = null,
    IReadOnlyList<PurchaseAdjustmentView>? Adjustments = null,
    int Version = 0);

public sealed record PurchaseEventView(PurchaseEventKind Kind, DateTimeOffset OccurredAt, string ActorCode, string Note);

public sealed record PurchaseAdjustmentView(
    Guid Id,
    string Label,
    decimal Amount,
    ChargeSplitMethod Method,
    string Reason,
    decimal AppliedToStock,
    decimal Variance,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed class CreatePurchaseLineInput
{
    /// <summary>Chỉ dùng khi sửa đơn: mã dòng đã có để giữ nguyên dòng đó.</summary>
    public Guid? LineId { get; set; }

    public Guid CatalogItemId { get; set; }

    public int Quantity { get; set; }

    public decimal FobUsd { get; set; }

    public decimal DutyRate { get; set; }

    public decimal SupplementalDuty { get; set; }

    public decimal SpecialConsumptionTax { get; set; }

    public decimal EnvironmentTax { get; set; }
}

public sealed class CreatePurchaseOrderCommand
{
    public Pancras.Domain.Employees.EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public string? Code { get; set; }

    public Guid SupplierId { get; set; }

    public Guid ReceiveLocationId { get; set; }

    public string? ContainerNumber { get; set; }

    public string? BillOfLading { get; set; }

    public string? Port { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public decimal ExchangeRate { get; set; }

    public DateOnly? ExchangeRateOn { get; set; }

    public string? ExchangeRateSource { get; set; }

    public decimal VatRate { get; set; }

    /// <summary>Theo khách hàng, VAT nhập khẩu tính vào giá vốn nên mặc định là không khấu trừ.</summary>
    public bool VatDeductible { get; set; }

    public IReadOnlyList<CreatePurchaseLineInput> Lines { get; set; } = [];

    /// <summary>Chi phí container ban đầu. Được thêm và chia ngay trước khi lưu, cùng một lần lưu với đơn.</summary>
    public IReadOnlyList<CreatePurchaseChargeInput> Charges { get; set; } = [];
}

public sealed class CreatePurchaseChargeInput
{
    public decimal Amount { get; set; }

    public string? Label { get; set; }

    public ChargeGroup Group { get; set; }

    /// <summary>Cách chia do người dùng chọn: trị giá FOB, số lượng, cân nặng hoặc thể tích. Không tự đổi nếu thiếu số đo.</summary>
    public ChargeSplitMethod Method { get; set; } = ChargeSplitMethod.FobValue;
}

/// <summary>Một dòng chi phí chung trong form sửa đơn. Có <see cref="ChargeId"/> là khoản đã có, không có là khoản mới.</summary>
public sealed class UpdatePurchaseChargeInput
{
    public Guid? ChargeId { get; set; }

    public decimal Amount { get; set; }

    public string? Label { get; set; }

    public ChargeGroup Group { get; set; }

    public ChargeSplitMethod Method { get; set; } = ChargeSplitMethod.FobValue;
}

public sealed class UpdatePurchaseOrderCommand
{
    public Pancras.Domain.Employees.EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public Guid PurchaseOrderId { get; set; }

    public string? Code { get; set; }

    public Guid SupplierId { get; set; }

    public Guid ReceiveLocationId { get; set; }

    public string? ContainerNumber { get; set; }

    public string? BillOfLading { get; set; }

    public string? Port { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public decimal ExchangeRate { get; set; }

    public DateOnly? ExchangeRateOn { get; set; }

    public string? ExchangeRateSource { get; set; }

    public decimal VatRate { get; set; }

    public bool VatDeductible { get; set; }

    /// <summary>Phiên bản đơn lúc mở form sửa. Lệch với đơn đang lưu thì từ chối.</summary>
    public int Version { get; set; }

    public IReadOnlyList<CreatePurchaseLineInput> Lines { get; set; } = [];

    /// <summary>
    /// Danh sách chi phí chung sau khi sửa. null = không đụng tới chi phí; có danh sách = danh sách đó là đúng:
    /// khoản đã có mà không còn trong danh sách thì bị xóa, dòng không có ChargeId là khoản mới.
    /// Khoản chia tay (Manual) chỉ được giữ hoặc xóa, không sửa số tiền/cách chia ở đây.
    /// </summary>
    public IReadOnlyList<UpdatePurchaseChargeInput>? Charges { get; set; }
}

/// <summary>
/// Một lần nhận hàng. <see cref="Actuals"/> là số thực nhận lần này theo mã dòng; null nghĩa là nhận đủ phần còn lại của mọi dòng.
/// <see cref="Final"/> = đây là lần nhận cuối (đóng đơn); sai thì đơn ở trạng thái "nhận một phần".
/// </summary>
public sealed record ReceivePurchaseOrderCommand(
    Guid PurchaseOrderId,
    Pancras.Domain.Employees.EmployeeActor Actor,
    IReadOnlyDictionary<Guid, int>? Actuals = null,
    bool Final = true,
    string? Note = null);

public enum PurchaseStatusAction
{
    Submit = 1,
    Approve = 2,
    ReturnToDraft = 3,
    MarkInTransit = 4,
    Cancel = 5
}

public sealed record ChangePurchaseStatusCommand(
    Guid PurchaseOrderId,
    Pancras.Domain.Employees.EmployeeActor Actor,
    PurchaseStatusAction Action,
    string? Note = null);

public sealed class AdjustPurchaseCostCommand
{
    public Pancras.Domain.Employees.EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public Guid PurchaseOrderId { get; set; }

    public string? Label { get; set; }

    /// <summary>Có dấu: dương là tăng giá vốn, âm là giảm.</summary>
    public decimal Amount { get; set; }

    public ChargeSplitMethod Method { get; set; } = ChargeSplitMethod.FobValue;

    public string? Reason { get; set; }
}

/// <summary>Tác động dự kiến lên giá vốn bình quân nếu nhận đủ phần còn lại của một dòng.</summary>
public sealed record PurchaseReceiptPreviewLine(Guid LineId, int Remaining, int OnHand, decimal CurrentCost, decimal ProjectedCost);
