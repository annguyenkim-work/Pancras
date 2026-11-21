using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ListInventoryDocumentsHandler(
    IInventoryRepository repository,
    ILogger<ListInventoryDocumentsHandler> logger,
    ITransferAttachmentRepository? transferAttachments = null,
    IStockDocumentAttachmentRepository? stockAttachments = null)
{
    public async Task<IReadOnlyList<StockLocationChoice>> ListLocationsAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var locations = await repository.ListLocationsAsync(cancellationToken);
        return locations
            .Where(location => InventoryAccess.Scope(actor) is not Guid scope || location.Id == scope)
            .OrderBy(location => location.Name, StringComparer.Ordinal)
            .Select(location => new StockLocationChoice(location.Id, location.Name, location.Kind))
            .ToArray();
    }

    /// <summary>Mọi địa điểm, dùng làm nơi nhận của phiếu chuyển. Chỉ có tên và loại.</summary>
    public async Task<IReadOnlyList<StockLocationChoice>> ListAllLocationsAsync(CancellationToken cancellationToken)
    {
        var locations = await repository.ListLocationsAsync(cancellationToken);
        return locations
            .OrderBy(location => location.Name, StringComparer.Ordinal)
            .Select(location => new StockLocationChoice(location.Id, location.Name, location.Kind))
            .ToArray();
    }

    public async Task<IReadOnlyList<StockItemChoice>> ListItemsAsync(CancellationToken cancellationToken)
    {
        var items = (await repository.ListItemChoicesAsync(cancellationToken)).Where(item => item.IsActive);
        return items.OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Một trang phiếu chuyển người dùng được thấy, đã lọc và cắt trong database (đang xử lý trước, mới nhất trước).
    /// Chỉ nạp dòng hàng, tệp đính kèm và lịch sử của các phiếu trong trang.
    /// </summary>
    public async Task<TransferListPage> ListTransferPageAsync(EmployeeActor actor, TransferListRequest request, CancellationToken cancellationToken)
    {
        var query = new TransferListQuery(
            TransferWorkflowAccess.Visible(actor),
            request.Waiting is TransferWaitingKind kind ? TransferWorkflowAccess.Waiting(actor, kind) : null,
            request.Status,
            request.LocationId,
            string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim(),
            request.CreatedFrom,
            request.CreatedBefore);
        var page = await repository.ListTransfersPageAsync(query, request.PageNumber, PageSizes.Normalize(request.PageSize), cancellationToken);
        var rows = await BuildTransferRowsAsync(actor, page.Items, cancellationToken);
        logger.LogInformation("Listed {TransferSlipCount} of {TransferSlipTotal} transfer slips.", rows.Count, page.Total);
        return new TransferListPage(rows, page.Total, page.PageNumber, page.AnyVisible);
    }

    /// <summary>Số phiếu người dùng được thấy và đang chờ chính họ xử lý ở nhóm <paramref name="kind"/> (đếm trong database).</summary>
    public Task<int> CountWaitingTransfersAsync(EmployeeActor actor, TransferWaitingKind kind, CancellationToken cancellationToken) =>
        repository.CountTransfersAsync(
            Predicates.And(TransferWorkflowAccess.Visible(actor), TransferWorkflowAccess.Waiting(actor, kind)),
            cancellationToken);

    /// <summary>Các phiếu theo Id mà người dùng được thấy (phiếu ngoài phạm vi coi như không tồn tại), dựng đủ như dòng của danh sách.</summary>
    public async Task<IReadOnlyList<TransferSlipRow>> GetTransfersAsync(EmployeeActor actor, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var slips = await repository.FindTransfersAsync(TransferWorkflowAccess.Visible(actor), ids.Distinct().ToArray(), cancellationToken);
        return await BuildTransferRowsAsync(actor, slips, cancellationToken);
    }

    private async Task<IReadOnlyList<TransferSlipRow>> BuildTransferRowsAsync(EmployeeActor actor, IReadOnlyList<TransferSlip> slips, CancellationToken cancellationToken)
    {
        if (slips.Count == 0)
        {
            return [];
        }

        var ids = slips.Select(slip => slip.Id).ToArray();
        var names = await NamesAsync(cancellationToken);
        var files = transferAttachments is null
            ? new Dictionary<Guid, IReadOnlyList<TransferAttachment>>()
            : (await transferAttachments.ListForSlipsAsync(ids, cancellationToken)).ToDictionary(pair => pair.Key, pair => pair.Value);
        var events = (await repository.ListTransferEventsAsync(ids, cancellationToken))
            .GroupBy(item => item.TransferSlipId)
            .ToDictionary(group => group.Key, group => group.ToList());
        return slips
            .Select(slip => new TransferSlipRow(
            slip.Id,
            names.Location(slip.SourceLocationId),
            names.Location(slip.DestinationLocationId),
            slip.Note,
            slip.Status,
            slip.Lines.Select(line => new TransferLineRow(
                line.CatalogItemId,
                names.Code(line.CatalogItemId),
                names.Item(line.CatalogItemId),
                line.Quantity,
                names.Unit(line.CatalogItemId),
                line.Id,
                line.ReceivedQuantity)).ToArray(),
            AuditStampView.From(slip),
            FlowOf(slip, actor, files.TryGetValue(slip.Id, out var attached) ? attached : [], events.TryGetValue(slip.Id, out var trail) ? trail : []),
            slip.SourceLocationId,
            slip.DestinationLocationId,
            slip.Number))
            .ToArray();
    }

    private static TransferFlowRow FlowOf(TransferSlip slip, EmployeeActor actor, IReadOnlyList<TransferAttachment> files, IReadOnlyList<TransferEvent> trail)
    {
        var touch = TransferAccess.CanTouch(actor, slip);
        var mine = touch && TransferAccess.IsCreator(actor, slip);
        return new TransferFlowRow(
            slip.SealNumber,
            slip.Carrier,
            slip.ReceiveNote,
            slip.ResolutionNote,
            slip.ApprovedBy,
            slip.ApprovedAt,
            slip.ShippedBy,
            slip.ShippedAt,
            slip.ReceivedBy,
            slip.ReceivedAt,
            slip.ResolvedBy,
            slip.ResolvedAt,
            slip.HasDiscrepancy,
            TransferWorkflowAccess.IsWaiting(actor, TransferWaitingKind.Decide, slip.Status, slip.SourceLocationId, slip.DestinationLocationId),
            slip.Status == TransferStatus.Requested && mine,
            TransferWorkflowAccess.IsWaiting(actor, TransferWaitingKind.Ship, slip.Status, slip.SourceLocationId, slip.DestinationLocationId),
            TransferWorkflowAccess.IsWaiting(actor, TransferWaitingKind.Receive, slip.Status, slip.SourceLocationId, slip.DestinationLocationId),
            slip.Status is TransferStatus.Requested or TransferStatus.Approved && mine,
            TransferWorkflowAccess.IsWaiting(actor, TransferWaitingKind.Resolve, slip.Status, slip.SourceLocationId, slip.DestinationLocationId),
            touch && slip.Status is not (TransferStatus.Cancelled or TransferStatus.Rejected),
            files.OrderBy(file => file.UploadedAt).Select(file => new TransferAttachmentView(file.Id, file.FileName, file.SizeBytes, file.UploadedAt, file.UploadedBy)).ToArray(),
            slip.Status == TransferStatus.InTransit && InventoryRoles.IsFinance(actor.Role),
            HistoryOf(slip, trail),
            TransferAccess.CanPrint(actor, slip));
    }

    /// <summary>
    /// Lịch sử phiếu từ bảng sự kiện. Phiếu lập trước khi có bảng này thì dựng lại từ các cột người/thời điểm từng bước.
    /// </summary>
    private static IReadOnlyList<TransferEventView> HistoryOf(TransferSlip slip, IReadOnlyList<TransferEvent> trail)
    {
        if (trail.Count > 0)
        {
            return trail.OrderBy(item => item.OccurredAt).Select(item => new TransferEventView(item.Kind, item.ActorCode, item.OccurredAt, item.Note)).ToArray();
        }

        var legacy = new List<TransferEventView> { new(TransferEventKind.Requested, slip.UpdatedBy, slip.UpdatedAt, slip.Note) };
        if (slip.ApprovedAt is DateTimeOffset approved)
        {
            legacy.Add(new(TransferEventKind.Approved, slip.ApprovedBy, approved, string.Empty));
        }

        if (slip.ShippedAt is DateTimeOffset shipped)
        {
            legacy.Add(new(TransferEventKind.Shipped, slip.ShippedBy, shipped, string.Empty));
        }

        if (slip.ReceivedAt is DateTimeOffset received)
        {
            legacy.Add(new(slip.HasDiscrepancy ? TransferEventKind.ReceivedWithDiscrepancy : TransferEventKind.Received, slip.ReceivedBy, received, slip.ReceiveNote));
        }

        if (slip.ResolvedAt is DateTimeOffset resolved)
        {
            legacy.Add(new(TransferEventKind.Resolved, slip.ResolvedBy, resolved, slip.ResolutionNote));
        }

        return legacy;
    }

    /// <summary>
    /// Một trang của tab Kiểm kê và điều chỉnh (kiểm kê, rồi điều chỉnh, rồi hủy; mỗi loại mới nhất trước), đếm và cắt trong database.
    /// <paramref name="focus"/> (phiếu mở từ thông báo) thì trả đúng trang chứa phiếu đó nếu phiếu khớp <paramref name="filter"/>; phiếu không khớp thì hiện danh sách theo bộ lọc.
    /// </summary>
    public async Task<StockDocumentPage> ListStockDocumentPageAsync(EmployeeActor actor, int pageNumber, int pageSize, Guid? focus, StockDocumentFilter filter, CancellationToken cancellationToken)
    {
        if (!InventoryAccess.CanOperate(actor.Role))
        {
            return new StockDocumentPage([], [], [], 0, 1);
        }

        var scope = InventoryAccess.Scope(actor);
        var counts = await repository.CountStockDocumentsAsync(scope, filter, cancellationToken);
        var position = focus is Guid id ? await repository.LocateStockDocumentAsync(scope, id, filter, cancellationToken) : null;
        var window = StockDocumentWindow.Compute(counts, pageNumber, PageSizes.Normalize(pageSize), position);
        var stocktakes = await StocktakeRowsAsync(actor, await repository.ListStocktakesAsync(scope, window.Stocktakes, filter, cancellationToken), cancellationToken);
        var adjustments = await AdjustmentRowsAsync(actor, await repository.ListAdjustmentsAsync(scope, window.Adjustments, filter, cancellationToken), cancellationToken);
        var disposals = await DisposalRowsAsync(actor, await repository.ListDisposalsAsync(scope, window.Disposals, filter, cancellationToken), cancellationToken);
        return new StockDocumentPage(stocktakes, adjustments, disposals, counts.Total, window.Page);
    }

    /// <summary>
    /// Nhãn cho các phiếu được nhắc tới (dòng lịch sử tồn, phiếu điều chỉnh tự sinh khi nhận lệch): mã và tuyến phiếu chuyển, lý do
    /// kiểm kê/điều chỉnh/hủy. Chỉ phiếu người dùng được thấy; phiếu ngoài phạm vi không có nhãn.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, StockDocumentLabel>> DescribeDocumentsAsync(EmployeeActor actor, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var labels = new Dictionary<Guid, StockDocumentLabel>();
        var wanted = ids.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (wanted.Length == 0)
        {
            return labels;
        }

        var slips = await repository.FindTransfersAsync(TransferWorkflowAccess.Visible(actor), wanted, cancellationToken);
        if (slips.Count > 0)
        {
            var names = await NamesAsync(cancellationToken);
            foreach (var slip in slips)
            {
                labels[slip.Id] = new StockDocumentLabel(StockDocumentLabelKind.Transfer, slip.Number, names.Location(slip.SourceLocationId), names.Location(slip.DestinationLocationId), slip.Note);
            }
        }

        if (InventoryAccess.CanOperate(actor.Role))
        {
            var (stocktakes, adjustments, disposals) = await repository.FindStockDocumentsAsync(InventoryAccess.Scope(actor), wanted, cancellationToken);
            foreach (var sheet in stocktakes)
            {
                labels.TryAdd(sheet.Id, new StockDocumentLabel(StockDocumentLabelKind.Stocktake, 0, string.Empty, string.Empty, sheet.Reason));
            }

            foreach (var adjustment in adjustments)
            {
                labels.TryAdd(adjustment.Id, new StockDocumentLabel(StockDocumentLabelKind.Adjustment, 0, string.Empty, string.Empty, adjustment.Reason));
            }

            foreach (var slip in disposals)
            {
                labels.TryAdd(slip.Id, new StockDocumentLabel(StockDocumentLabelKind.Disposal, 0, string.Empty, string.Empty, slip.Reason));
            }
        }

        return labels;
    }

    private async Task<IReadOnlyList<StocktakeRow>> StocktakeRowsAsync(EmployeeActor actor, IReadOnlyList<StocktakeSheet> sheets, CancellationToken cancellationToken)
    {
        var files = await FilesAsync(sheets.Select(sheet => sheet.Id), cancellationToken);
        var names = await NamesAsync(cancellationToken);
        return sheets.Select(sheet => new StocktakeRow(
            sheet.Id,
            names.Location(sheet.StockLocationId),
            names.Code(sheet.CatalogItemId),
            names.Item(sheet.CatalogItemId),
            sheet.BookQuantity,
            sheet.CountedQuantity,
            sheet.Variance,
            sheet.Reason,
            sheet.DocumentRef,
            sheet.Status,
            names.Unit(sheet.CatalogItemId),
            sheet.Status == ApprovalStatus.Pending && InventoryAccess.CanApprove(actor, sheet.CreatedByRole, sheet.StockLocationId),
            AuditStampView.From(sheet),
            files.GetValueOrDefault(sheet.Id))).ToArray();
    }

    private async Task<IReadOnlyList<AdjustmentRow>> AdjustmentRowsAsync(EmployeeActor actor, IReadOnlyList<QuantityAdjustment> adjustments, CancellationToken cancellationToken)
    {
        var files = await FilesAsync(adjustments.Select(adjustment => adjustment.Id), cancellationToken);
        var names = await NamesAsync(cancellationToken);
        return adjustments.Select(adjustment => new AdjustmentRow(
            adjustment.Id,
            names.Location(adjustment.StockLocationId),
            names.Code(adjustment.CatalogItemId),
            names.Item(adjustment.CatalogItemId),
            adjustment.Delta,
            adjustment.Reason,
            adjustment.DocumentRef,
            adjustment.Status,
            adjustment.Status == ApprovalStatus.Pending && InventoryAccess.CanApproveAdjustment(actor.Role),
            AuditStampView.From(adjustment),
            adjustment.TransferSlipId,
            files.GetValueOrDefault(adjustment.Id))).ToArray();
    }

    private async Task<IReadOnlyList<DisposalRow>> DisposalRowsAsync(EmployeeActor actor, IReadOnlyList<DisposalSlip> slips, CancellationToken cancellationToken)
    {
        var files = await FilesAsync(slips.Select(slip => slip.Id), cancellationToken);
        var names = await NamesAsync(cancellationToken);
        return slips.Select(slip => new DisposalRow(
            slip.Id,
            names.Location(slip.StockLocationId),
            names.Code(slip.CatalogItemId),
            names.Item(slip.CatalogItemId),
            slip.Reason,
            slip.DocumentRef,
            slip.Status,
            slip.Status == ApprovalStatus.Pending && InventoryAccess.CanApprove(actor, slip.CreatedByRole, slip.StockLocationId),
            AuditStampView.From(slip),
            files.GetValueOrDefault(slip.Id),
            slip.Quantity)).ToArray();
    }

    private async Task<Dictionary<Guid, IReadOnlyList<TransferAttachmentView>>> FilesAsync(IEnumerable<Guid> documentIds, CancellationToken cancellationToken)
    {
        var ids = documentIds.ToArray();
        if (stockAttachments is null || ids.Length == 0)
        {
            return [];
        }

        return (await stockAttachments.ListForDocumentsAsync(ids, cancellationToken)).ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<TransferAttachmentView>)pair.Value.OrderBy(file => file.UploadedAt).Select(file => new TransferAttachmentView(file.Id, file.FileName, file.SizeBytes, file.UploadedAt, file.UploadedBy)).ToArray());
    }

    private async Task<NameMap> NamesAsync(CancellationToken cancellationToken)
    {
        var locations = await repository.ListLocationsAsync(cancellationToken);
        var items = await repository.ListItemChoicesAsync(cancellationToken);
        var deleted = await repository.ListDeletedLocationsAsync(cancellationToken);
        return new NameMap(locations, deleted, items);
    }

    internal const string DeletedSuffix = " (đã xóa)";

    private sealed class NameMap
    {
        private readonly Dictionary<Guid, string> _locations;
        private readonly Dictionary<Guid, StockItemChoice> _items;

        public NameMap(IReadOnlyList<StockLocation> locations, IReadOnlyList<StockLocation> deleted, IReadOnlyList<StockItemChoice> items)
        {
            _locations = deleted.ToDictionary(location => location.Id, location => location.Name + DeletedSuffix);
            foreach (var location in locations)
            {
                _locations[location.Id] = location.Name;
            }

            _items = items.ToDictionary(item => item.Id);
        }

        public string Location(Guid id) => _locations.TryGetValue(id, out var name) ? name : string.Empty;

        public string Code(Guid id) => _items.TryGetValue(id, out var item) ? item.Code : string.Empty;

        public string Item(Guid id) => _items.TryGetValue(id, out var item) ? item.Name : string.Empty;

        public string Unit(Guid id) => _items.TryGetValue(id, out var item) ? item.Unit : string.Empty;
    }
}
