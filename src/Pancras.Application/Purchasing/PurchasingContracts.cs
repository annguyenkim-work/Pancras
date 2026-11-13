using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken);

    Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(PurchaseOrder order, CancellationToken cancellationToken);

    Task<bool> CommitAsync(CancellationToken cancellationToken);
}

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
    decimal LandedTotal);

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
    decimal? DeclaredCustomsValue,
    decimal? DeclaredDuty,
    decimal? DeclaredVat,
    PurchaseLineCost Cost);

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
    Guid SupplierId = default);

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
    public string? Code { get; set; }

    public Guid SupplierId { get; set; }

    public Guid ReceiveLocationId { get; set; }

    public string? ContainerNumber { get; set; }

    public string? BillOfLading { get; set; }

    public string? Port { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal VatRate { get; set; }

    /// <summary>Theo khách hàng, VAT nhập khẩu tính vào giá vốn nên mặc định là không khấu trừ.</summary>
    public bool VatDeductible { get; set; }

    public IReadOnlyList<CreatePurchaseLineInput> Lines { get; set; } = [];
}

public sealed class UpdatePurchaseOrderCommand
{
    public Guid PurchaseOrderId { get; set; }

    public string? Code { get; set; }

    public Guid SupplierId { get; set; }

    public Guid ReceiveLocationId { get; set; }

    public string? ContainerNumber { get; set; }

    public string? BillOfLading { get; set; }

    public string? Port { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal VatRate { get; set; }

    public bool VatDeductible { get; set; }

    public IReadOnlyList<CreatePurchaseLineInput> Lines { get; set; } = [];
}

public sealed record DeletePurchaseChargeCommand(Guid PurchaseOrderId, Guid ChargeId);

public sealed class AllocateChargeCommand
{
    public Guid PurchaseOrderId { get; set; }

    public decimal Amount { get; set; }

    public ChargeSplitMethod Method { get; set; }

    public string? Label { get; set; }

    /// <summary>Nếu có, khoản được gán nhóm ngay trong cùng một lần lưu.</summary>
    public ChargeGroup? Group { get; set; }

    public IReadOnlyDictionary<Guid, decimal> ManualShares { get; set; } = new Dictionary<Guid, decimal>();
}

public sealed class AssignChargeGroupCommand
{
    public Guid PurchaseOrderId { get; set; }

    public Guid ChargeId { get; set; }

    public ChargeGroup Group { get; set; }

    public bool RetainCurrent { get; set; }
}

public sealed class RecordCustomsDeclarationCommand
{
    public Guid PurchaseOrderId { get; set; }

    public Guid LineId { get; set; }

    public decimal? DeclaredCustomsValue { get; set; }

    public decimal? DeclaredDuty { get; set; }

    public decimal? DeclaredVat { get; set; }

    public bool HasCustoms { get; set; }

    public bool HasDuty { get; set; }

    public bool HasVat { get; set; }
}

public sealed record ReceivePurchaseOrderCommand(Guid PurchaseOrderId);
