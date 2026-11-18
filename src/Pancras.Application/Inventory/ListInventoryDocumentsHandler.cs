using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
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

    public async Task<IReadOnlyList<TransferSlipRow>> ListTransfersAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var slips = InventoryAccess.CanOperate(actor.Role)
            ? (await repository.ListTransfersAsync(cancellationToken))
                .Where(slip => InventoryAccess.CanAccessLocation(actor, slip.SourceLocationId) || InventoryAccess.CanAccessLocation(actor, slip.DestinationLocationId))
                .ToArray()
            : [];
        var names = await NamesAsync(cancellationToken);
        var files = transferAttachments is null || slips.Length == 0
            ? new Dictionary<Guid, IReadOnlyList<TransferAttachment>>()
            : (await transferAttachments.ListForSlipsAsync(slips.Select(slip => slip.Id).ToArray(), cancellationToken)).ToDictionary(pair => pair.Key, pair => pair.Value);
        var events = slips.Length == 0
            ? new Dictionary<Guid, List<TransferEvent>>()
            : (await repository.ListTransferEventsAsync(slips.Select(slip => slip.Id).ToArray(), cancellationToken))
                .GroupBy(item => item.TransferSlipId)
                .ToDictionary(group => group.Key, group => group.ToList());
        logger.LogInformation("Listed {TransferSlipCount} transfer slips.", slips.Length);
        return slips
            .OrderBy(slip => slip.Status is TransferStatus.Requested or TransferStatus.Approved or TransferStatus.InTransit or TransferStatus.ReceivedWithDiscrepancy ? 0 : 1)
            .ThenByDescending(slip => slip.Id)
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
        var atSource = InventoryAccess.CanOperate(actor.Role) && InventoryAccess.CanAccessLocation(actor, slip.SourceLocationId);
        var atDestination = InventoryAccess.CanOperate(actor.Role) && InventoryAccess.CanAccessLocation(actor, slip.DestinationLocationId);
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
            slip.Status == TransferStatus.Requested && DecideTransferCommandHandler.CanDecide(actor, slip.SourceLocationId),
            slip.Status == TransferStatus.Requested && mine,
            slip.Status == TransferStatus.Approved && atSource,
            slip.Status == TransferStatus.InTransit && atDestination,
            slip.Status is TransferStatus.Requested or TransferStatus.Approved && mine,
            slip.Status == TransferStatus.ReceivedWithDiscrepancy && InventoryRoles.IsFinance(actor.Role),
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

    public async Task<IReadOnlyList<StocktakeRow>> ListStocktakesAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var sheets = InventoryAccess.CanOperate(actor.Role)
            ? (await repository.ListStocktakesAsync(cancellationToken)).Where(sheet => InventoryAccess.CanAccessLocation(actor, sheet.StockLocationId)).ToArray()
            : [];
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

    public async Task<IReadOnlyList<AdjustmentRow>> ListAdjustmentsAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var adjustments = InventoryAccess.CanOperate(actor.Role)
            ? (await repository.ListAdjustmentsAsync(cancellationToken)).Where(item => InventoryAccess.CanAccessLocation(actor, item.StockLocationId)).ToArray()
            : [];
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

    public async Task<IReadOnlyList<DisposalRow>> ListDisposalsAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        var slips = InventoryAccess.CanOperate(actor.Role)
            ? (await repository.ListDisposalsAsync(cancellationToken)).Where(item => InventoryAccess.CanAccessLocation(actor, item.StockLocationId)).ToArray()
            : [];
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
