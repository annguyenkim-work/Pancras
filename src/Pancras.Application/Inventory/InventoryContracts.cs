using Pancras.Application.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public interface IInventoryRepository
{
    Task<IReadOnlyList<StockLocation>> ListLocationsAsync(CancellationToken cancellationToken);

    /// <summary>Kho đã xóa mềm. Chỉ để hiển thị tên trên phiếu và lịch sử cũ, không dùng để chọn kho.</summary>
    Task<IReadOnlyList<StockLocation>> ListDeletedLocationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StockItemChoice>> ListItemChoicesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StockBalanceRow>> ListBalancesAsync(StockListFilter filter, CancellationToken cancellationToken);

    Task<StockBalance?> FindBalanceForUpdateAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken);

    Task<int> SumOnHandAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken);

    Task AddTransferAsync(TransferSlip slip, CancellationToken cancellationToken);

    Task<TransferSlip?> FindTransferForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid?> FindTransferIdByRequestKeyAsync(Guid requestKey, Guid requestedById, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferSlip>> ListOpenTransfersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferSlip>> ListTransfersAsync(CancellationToken cancellationToken);

    Task AddTransferEventAsync(TransferEvent transferEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferEvent>> ListTransferEventsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken);

    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> ListHistoryAsync(Guid catalogItemId, Guid stockLocationId, CancellationToken cancellationToken);

    Task<StockItemProfile?> FindItemProfileAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockBalanceRow>> ListItemBalancesAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockMovement>> ListItemMovementsAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task AddStocktakeAsync(StocktakeSheet sheet, CancellationToken cancellationToken);

    Task<StocktakeSheet?> FindStocktakeForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StocktakeSheet>> ListStocktakesAsync(CancellationToken cancellationToken);

    Task AddAdjustmentAsync(QuantityAdjustment adjustment, CancellationToken cancellationToken);

    Task<QuantityAdjustment?> FindAdjustmentForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<QuantityAdjustment>> ListAdjustmentsAsync(CancellationToken cancellationToken);

    Task AddDisposalAsync(DisposalSlip slip, CancellationToken cancellationToken);

    Task<DisposalSlip?> FindDisposalForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DisposalSlip>> ListDisposalsAsync(CancellationToken cancellationToken);

    Task AddNoticeAsync(StockNotice notice, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockNotice>> ListNoticesAsync(Guid recipientEmployeeId, CancellationToken cancellationToken);

    Task<StockNotice?> FindNoticeForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task RemoveNoticesAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Kho của một biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy. Chỉ đọc, không khóa dòng và không mở transaction,
    /// để các thao tác phụ (đính kèm, tải file) không để lại transaction treo.
    /// </summary>
    Task<Guid?> FindDocumentLocationAsync(StockDocumentKind kind, Guid id, CancellationToken cancellationToken);

    Task<bool> CommitAsync(CancellationToken cancellationToken);
}

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

public sealed record StockListFilter(Guid? LocationId, bool LocationRejected, ProductCategory? Category, bool CategoryRejected, string? Term)
{
    public static StockListFilter From(string? location, string? categoryToken, string? term)
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
            return new StockListFilter(locationId, locationRejected, null, false, normalizedTerm);
        }

        if (Catalog.ProductCategoryToken.TryParse(categoryToken, out var category))
        {
            return new StockListFilter(locationId, locationRejected, category, false, normalizedTerm);
        }

        return new StockListFilter(locationId, locationRejected, null, true, normalizedTerm);
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
