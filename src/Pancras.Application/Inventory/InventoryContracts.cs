using Pancras.Application.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed record StockBalanceRow(
    Guid CatalogItemId,
    string Code,
    string Name,
    string Brand,
    ProductCategory Category,
    Guid StockLocationId,
    string LocationName,
    int OnHand,
    int Reserved,
    int Available,
    int InTransit,
    string Unit = "",
    string Origin = "",
    decimal CostPrice = 0);

public sealed record StockItemProfile(
    Guid Id,
    string Code,
    string Name,
    ProductCategory Category,
    string Brand,
    string Origin,
    string Unit,
    decimal? WeightKg,
    decimal? VolumeM3,
    int? WarrantyMonths,
    decimal? MinimumStock,
    decimal CostPrice,
    decimal RetailPrice,
    decimal DealerPrice,
    decimal DistributorPrice,
    decimal ProjectPrice);

public sealed record ListStockBalancesQuery(Guid? LocationId, bool LocationRejected, ProductCategory? Category, bool CategoryRejected, string? Term)
{
    public static ListStockBalancesQuery From(string? location, string? categoryToken, string? term)
    {
        var normalizedTerm = Normalize(term);
        Guid? locationId = null;
        var locationRejected = false;
        if (!string.IsNullOrWhiteSpace(location))
        {
            if (Guid.TryParse(location.Trim(), out var parsed) && parsed != Guid.Empty)
            {
                locationId = parsed;
            }
            else
            {
                locationRejected = true;
            }
        }

        if (string.IsNullOrWhiteSpace(categoryToken))
        {
            return new ListStockBalancesQuery(locationId, locationRejected, null, false, normalizedTerm);
        }

        if (Catalog.ProductCategoryToken.TryParse(categoryToken, out var category))
        {
            return new ListStockBalancesQuery(locationId, locationRejected, category, false, normalizedTerm);
        }

        return new ListStockBalancesQuery(locationId, locationRejected, null, true, normalizedTerm);
    }

    public bool Matches(StockBalanceRow row)
    {
        if (LocationRejected || CategoryRejected)
        {
            return false;
        }

        if (LocationId is Guid locationId && row.StockLocationId != locationId)
        {
            return false;
        }

        if (Category is ProductCategory category && row.Category != category)
        {
            return false;
        }

        if (Term is null)
        {
            return true;
        }

        return row.Code.Contains(Term, StringComparison.OrdinalIgnoreCase)
            || row.Name.Contains(Term, StringComparison.OrdinalIgnoreCase)
            || row.Brand.Contains(Term, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

public sealed record TransferLineInput(Guid CatalogItemId, int Quantity);

public sealed class CreateTransferSlipCommand
{
    public Guid SourceLocationId { get; set; }

    public Guid DestinationLocationId { get; set; }

    public string? Note { get; set; }

    /// <summary>Mã do form sinh ra khi mở; cùng mã gửi lại thì trả về phiếu đã tạo.</summary>
    public Guid? RequestKey { get; set; }

    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public IReadOnlyList<TransferLineInput> Lines { get; set; } = [];
}

public sealed record ConfirmTransferCommand(
    Guid TransferSlipId,
    EmployeeActor Actor,
    IReadOnlyList<ReceivedLineInput>? Actuals = null,
    string? Note = null);

public sealed class CreateStocktakeCommand
{
    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public int CountedQuantity { get; set; }

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }

    public string? ImageRef { get; set; }
}

public sealed class CreateQuantityAdjustmentCommand
{
    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public int Delta { get; set; }

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }

    public string? ImageRef { get; set; }
}

public sealed class CreateDisposalSlipCommand
{
    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }

    public string? ImageRef { get; set; }
}

public sealed record ApproveDocumentCommand(Guid Id, EmployeeActor Actor);

public sealed record StockHistoryRow(StockMovementKind Kind, int Quantity, DateTimeOffset OccurredAt, Guid DocumentId);

public sealed record StockHistoryEntry(
    StockMovementKind Kind,
    int Quantity,
    DateTimeOffset OccurredAt,
    Guid DocumentId,
    Guid StockLocationId,
    string LocationName);

public sealed record StockItemDetail(
    StockItemProfile Profile,
    IReadOnlyList<StockBalanceRow> Balances,
    IReadOnlyList<StockHistoryEntry> History);

public sealed record StockLocationChoice(Guid Id, string Name, StockLocationKind Kind);

public sealed record StockItemChoice(Guid Id, string Code, string Name, string Unit = "", bool IsActive = true);

public sealed record TransferLineRow(Guid CatalogItemId, string Code, string Name, int Quantity, string Unit = "", Guid LineId = default, int? ReceivedQuantity = null);

public sealed record TransferSlipRow(
    Guid Id,
    string SourceName,
    string DestinationName,
    string Note,
    TransferStatus Status,
    IReadOnlyList<TransferLineRow> Lines,
    AuditStampView? Stamp = null,
    TransferFlowRow? Flow = null,
    Guid SourceId = default,
    Guid DestinationId = default);

/// <summary>Thông tin quy trình của phiếu chuyển: ai làm bước nào, số seal, lý do chênh lệch, file đính kèm và nút nào được phép bấm.</summary>
public sealed record TransferFlowRow(
    string SealNumber,
    string Carrier,
    string ReceiveNote,
    string ResolutionNote,
    string ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string ShippedBy,
    DateTimeOffset? ShippedAt,
    string ReceivedBy,
    DateTimeOffset? ReceivedAt,
    string ResolvedBy,
    DateTimeOffset? ResolvedAt,
    bool HasDiscrepancy,
    bool CanDecide,
    bool CanEdit,
    bool CanShip,
    bool CanReceive,
    bool CanCancel,
    bool CanResolve,
    bool CanAttach,
    IReadOnlyList<TransferAttachmentView> Attachments,
    bool CanReturn = false,
    IReadOnlyList<TransferEventView>? History = null);

/// <summary>Một dòng lịch sử phiếu chuyển: ai làm gì, lúc nào, lý do nếu có.</summary>
public sealed record TransferEventView(TransferEventKind Kind, string ActorCode, DateTimeOffset At, string Note);

public sealed record StocktakeRow(
    Guid Id,
    string LocationName,
    string Code,
    string Name,
    int BookQuantity,
    int CountedQuantity,
    int Variance,
    string Reason,
    string DocumentRef,
    ApprovalStatus Status,
    string Unit = "",
    bool CanApprove = false,
    AuditStampView? Stamp = null,
    IReadOnlyList<TransferAttachmentView>? Files = null,
    int Quantity = 1);

public sealed record AdjustmentRow(
    Guid Id,
    string LocationName,
    string Code,
    string Name,
    int Delta,
    string Reason,
    string DocumentRef,
    ApprovalStatus Status,
    bool CanApprove = false,
    AuditStampView? Stamp = null,
    Guid? TransferSlipId = null,
    IReadOnlyList<TransferAttachmentView>? Files = null);

public sealed record DisposalRow(
    Guid Id,
    string LocationName,
    string Code,
    string Name,
    string Reason,
    string DocumentRef,
    ApprovalStatus Status,
    bool CanApprove = false,
    AuditStampView? Stamp = null,
    IReadOnlyList<TransferAttachmentView>? Files = null,
    int Quantity = 1);
