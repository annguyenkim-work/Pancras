using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;
using Pancras.Web.Formatting;

namespace Pancras.Web.Pages.Inventory;

[Authorize]
public class IndexModel(
    ListStockBalancesHandler listHandler,
    ListInventoryDocumentsHandler documentsHandler,
    GetStockItemDetailHandler detailHandler,
    CreateTransferSlipCommandHandler createTransferHandler,
    CreateAndShipTransferCommandHandler createAndShipHandler,
    ConfirmTransferShipmentCommandHandler shipmentHandler,
    ConfirmTransferReceiptCommandHandler receiptHandler,
    CreateStocktakeCommandHandler createStocktakeHandler,
    ApproveStocktakeCommandHandler approveStocktakeHandler,
    CreateQuantityAdjustmentCommandHandler createAdjustmentHandler,
    ApproveQuantityAdjustmentCommandHandler approveAdjustmentHandler,
    CreateDisposalSlipCommandHandler createDisposalHandler,
    ApproveDisposalSlipCommandHandler approveDisposalHandler,
    ListStockNoticesHandler noticesHandler) : PageModel
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public IReadOnlyList<StockItemGroup> Groups { get; private set; } = [];

    public IReadOnlyList<StockLocationChoice> Locations { get; private set; } = [];

    public IReadOnlyList<StockItemChoice> Items { get; private set; } = [];

    public IReadOnlyList<TransferSlipRow> Transfers { get; private set; } = [];

    public IReadOnlyList<StocktakeRow> Stocktakes { get; private set; } = [];

    public IReadOnlyList<AdjustmentRow> Adjustments { get; private set; } = [];

    public IReadOnlyList<DisposalRow> Disposals { get; private set; } = [];

    public IReadOnlyList<StockNotice> Notices { get; private set; } = [];

    public bool CanCountStock => InventoryRoles.IsWarehouseKeeper(Role());

    public bool CanApproveStocktake => InventoryRoles.IsShowroomLead(Role());

    public bool CanDraftAdjustment => InventoryRoles.CanDraftAdjustment(Role());

    public bool CanPostAdjustment => InventoryRoles.CanPostAdjustment(Role());

    public bool CanApproveDisposal => InventoryRoles.IsShowroomLead(Role()) || InventoryRoles.IsDirector(Role());

    public StockItemDetail? Detail { get; private set; }

    public IReadOnlyList<StockHistoryEntry> VisibleHistory { get; private set; } = [];

    public int HistoryTotal { get; private set; }

    public int HistoryFrom { get; private set; }

    public int HistoryTo { get; private set; }

    public string BalanceJson { get; private set; } = "[]";

    public IReadOnlyDictionary<Guid, string> PlaceCodes { get; private set; } = new Dictionary<Guid, string>();

    public string? ErrorMessage { get; private set; }

    public bool ShowTransferDialog { get; private set; }

    public bool ShowStocktakeDialog { get; private set; }

    public int MovingCount => Transfers.Count(slip => slip.Status == TransferStatus.InTransit);

    public string ActiveView => View switch
    {
        "transfers" => "transfers",
        "stocktakes" => "stocktakes",
        _ => "balances"
    };

    public string Subtitle
    {
        get
        {
            var warehouses = Locations.Where(location => location.Kind == StockLocationKind.Warehouse).Select(location => location.Name).ToArray();
            var showrooms = Locations.Where(location => location.Kind == StockLocationKind.Showroom).Select(location => location.Name).ToArray();
            if (warehouses.Length == 0 && showrooms.Length == 0)
            {
                return "Chưa có địa điểm kho hoặc showroom.";
            }

            var warehouseText = warehouses.Length == 0
                ? "chưa có kho tổng"
                : $"{warehouses.Length} kho tổng ({string.Join(", ", warehouses)})";
            var showroomText = showrooms.Length == 0
                ? "chưa có showroom"
                : $"{showrooms.Length} showroom ({string.Join(", ", showrooms)})";
            return $"Quản lý {warehouseText} và {showroomText}.";
        }
    }

    public string LocationOptionLabel => Locations.Count == 0
        ? "Toàn hệ thống"
        : $"Toàn hệ thống ({Locations.Count} địa điểm)";

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "location")]
    public string? Location { get; set; }

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? View { get; set; }

    [BindProperty(SupportsGet = true, Name = "item")]
    public string? Item { get; set; }

    [BindProperty(SupportsGet = true, Name = "kind")]
    public string? HistoryKind { get; set; }

    [BindProperty(SupportsGet = true, Name = "place")]
    public string? HistoryPlace { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int HistoryPage { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "size")]
    public int HistorySize { get; set; } = 10;

    [BindProperty]
    public TransferInput Transfer { get; set; } = new();

    [BindProperty]
    public StocktakeInput Stocktake { get; set; } = new();

    [BindProperty]
    public AdjustmentInput Adjustment { get; set; } = new();

    [BindProperty]
    public DisposalInput Disposal { get; set; } = new();

    [BindProperty]
    public Guid DocumentId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        var rows = await listHandler.ExportAsync(CurrentFilter(), cancellationToken);
        return File(StockCsv.Write(rows), "text/csv; charset=utf-8", "ton-kho.csv");
    }

    public async Task<IActionResult> OnPostShipTransferAsync(CancellationToken cancellationToken)
    {
        var lines = (Transfer.Lines ?? [])
            .Where(line => line.CatalogItemId != Guid.Empty && line.Quantity > 0)
            .Select(line => new TransferLineInput(line.CatalogItemId, line.Quantity))
            .ToArray();
        var created = await createAndShipHandler.HandleAsync(
            new CreateTransferSlipCommand
            {
                SourceLocationId = Transfer.SourceLocationId,
                DestinationLocationId = Transfer.DestinationLocationId,
                Note = Transfer.Note,
                Lines = lines
            },
            cancellationToken);
        if (created.IsFailure || created.Value == Guid.Empty)
        {
            return await FailAsync(created.StockError, openTransfer: true, "transfers", cancellationToken);
        }

        var shipped = await shipmentHandler.HandleAsync(new ConfirmTransferCommand(created.Value), cancellationToken);
        if (shipped.IsFailure)
        {
            return await FailAsync(shipped.StockError, openTransfer: true, "transfers", cancellationToken);
        }

        View = "transfers";
        return Redirect(PageUrl(includeItem: false));
    }

    public Task<IActionResult> OnPostCreateTransferAsync(CancellationToken cancellationToken) =>
        SaveAsync(
            createTransferHandler.HandleAsync(
                new CreateTransferSlipCommand
                {
                    SourceLocationId = Transfer.SourceLocationId,
                    DestinationLocationId = Transfer.DestinationLocationId,
                    Note = Transfer.Note,
                    Lines = (Transfer.Lines ?? [])
                        .Where(line => line.CatalogItemId != Guid.Empty && line.Quantity > 0)
                        .Select(line => new TransferLineInput(line.CatalogItemId, line.Quantity))
                        .ToArray()
                },
                cancellationToken),
            "transfers",
            cancellationToken);

    public Task<IActionResult> OnPostConfirmShipmentAsync(CancellationToken cancellationToken) =>
        SaveAsync(shipmentHandler.HandleAsync(new ConfirmTransferCommand(DocumentId), cancellationToken), "transfers", cancellationToken);

    public Task<IActionResult> OnPostConfirmReceiptAsync(CancellationToken cancellationToken) =>
        SaveAsync(receiptHandler.HandleAsync(new ConfirmTransferCommand(DocumentId), cancellationToken), "transfers", cancellationToken);

    public Task<IActionResult> OnPostCreateStocktakeAsync(CancellationToken cancellationToken) =>
        SaveStocktakeAsync(
            createStocktakeHandler.HandleAsync(
                new CreateStocktakeCommand
                {
                    Role = Role(),
                    StockLocationId = Stocktake.StockLocationId,
                    CatalogItemId = Stocktake.CatalogItemId,
                    CountedQuantity = Stocktake.CountedQuantity,
                    Reason = Stocktake.Reason,
                    DocumentRef = Stocktake.DocumentRef,
                    ImageRef = Stocktake.ImageRef
                },
                cancellationToken),
            cancellationToken);

    public Task<IActionResult> OnPostApproveStocktakeAsync(CancellationToken cancellationToken) =>
        SaveAsync(approveStocktakeHandler.HandleAsync(new ApproveDocumentCommand(DocumentId, Role()), cancellationToken), "stocktakes", cancellationToken);

    public Task<IActionResult> OnPostCreateAdjustmentAsync(CancellationToken cancellationToken) =>
        SaveAsync(
            createAdjustmentHandler.HandleAsync(
                new CreateQuantityAdjustmentCommand
                {
                    Role = Role(),
                    StockLocationId = Adjustment.StockLocationId,
                    CatalogItemId = Adjustment.CatalogItemId,
                    Delta = Adjustment.Delta,
                    Reason = Adjustment.Reason,
                    DocumentRef = Adjustment.DocumentRef,
                    ImageRef = Adjustment.ImageRef
                },
                cancellationToken),
            "stocktakes",
            cancellationToken);

    public Task<IActionResult> OnPostApproveAdjustmentAsync(CancellationToken cancellationToken) =>
        SaveAsync(approveAdjustmentHandler.HandleAsync(new ApproveDocumentCommand(DocumentId, Role()), cancellationToken), "stocktakes", cancellationToken);

    public Task<IActionResult> OnPostCreateDisposalAsync(CancellationToken cancellationToken) =>
        SaveAsync(
            createDisposalHandler.HandleAsync(
                new CreateDisposalSlipCommand
                {
                    StockLocationId = Disposal.StockLocationId,
                    CatalogItemId = Disposal.CatalogItemId,
                    Reason = Disposal.Reason,
                    DocumentRef = Disposal.DocumentRef,
                    ImageRef = Disposal.ImageRef
                },
                cancellationToken),
            "stocktakes",
            cancellationToken);

    public async Task<IActionResult> OnPostOpenNoticeAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        StockNotice? notice = null;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId))
        {
            notice = await noticesHandler.OpenAsync(noticeId, employeeId, cancellationToken);
        }

        var target = notice is null
            ? "/inventory?view=stocktakes"
            : $"/inventory?view=stocktakes#document-{notice.DocumentId:D}";
        return Redirect(target);
    }

    public Task<IActionResult> OnPostApproveDisposalAsync(CancellationToken cancellationToken) =>
        SaveAsync(approveDisposalHandler.HandleAsync(new ApproveDocumentCommand(DocumentId, Role()), cancellationToken), "stocktakes", cancellationToken);

    public string ViewUrl(string view) => BuildUrl(view, item: null, kind: null, place: null, page: null, size: null);

    public string ItemUrl(Guid itemId) => BuildUrl("balances", itemId.ToString(), HistoryKind, HistoryPlace, 1, NormalizedSize());

    public string HistoryKindUrl(string kind) => BuildUrl(ActiveView, Item, kind, HistoryPlace, 1, NormalizedSize());

    public string HistoryPageUrl(int page) => BuildUrl(ActiveView, Item, HistoryKind, HistoryPlace, page, NormalizedSize());

    public string CloseUrl() => BuildUrl(ActiveView, null, null, null, null, null);

    public string Describe(StockHistoryEntry entry)
    {
        var transfer = Transfers.FirstOrDefault(slip => slip.Id == entry.DocumentId);
        if (transfer is not null)
        {
            var route = entry.Quantity < 0
                ? "Chuyển sang " + transfer.DestinationName
                : "Nhận từ " + transfer.SourceName;
            return string.IsNullOrWhiteSpace(transfer.Note) ? route : route + ". " + transfer.Note;
        }

        var stocktake = Stocktakes.FirstOrDefault(sheet => sheet.Id == entry.DocumentId);
        if (stocktake is not null)
        {
            return string.IsNullOrWhiteSpace(stocktake.Reason) ? "Kiểm kê" : "Kiểm kê. " + stocktake.Reason;
        }

        var adjustment = Adjustments.FirstOrDefault(sheet => sheet.Id == entry.DocumentId);
        if (adjustment is not null)
        {
            return string.IsNullOrWhiteSpace(adjustment.Reason) ? "Điều chỉnh tồn" : "Điều chỉnh tồn. " + adjustment.Reason;
        }

        var disposal = Disposals.FirstOrDefault(slip => slip.Id == entry.DocumentId);
        if (disposal is not null)
        {
            return string.IsNullOrWhiteSpace(disposal.Reason) ? "Xuất hủy" : "Xuất hủy. " + disposal.Reason;
        }

        return entry.Kind switch
        {
            StockMovementKind.Receipt => "Nhập kho",
            StockMovementKind.Issue => "Xuất kho",
            StockMovementKind.Adjustment => "Điều chỉnh tồn",
            _ => "Chuyển kho"
        };
    }

    public static string Count(int value) => value.ToString("N0", Vietnamese);

    public static string When(Guid id)
    {
        var time = StockLabels.Version7Time(id);
        return time is null ? string.Empty : time.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Vietnamese);
    }

    public string PlaceCode(Guid locationId) =>
        PlaceCodes.TryGetValue(locationId, out var code) ? code : string.Empty;

    private async Task<IActionResult> SaveAsync(Task<Result<Guid>> operation, string view, CancellationToken cancellationToken)
    {
        var result = await operation;
        if (result.IsFailure)
        {
            return await FailAsync(result.StockError, openTransfer: false, view, cancellationToken);
        }

        View = view;
        return Redirect(PageUrl(includeItem: false));
    }

    private async Task<IActionResult> SaveStocktakeAsync(Task<Result<Guid>> operation, CancellationToken cancellationToken)
    {
        var result = await operation;
        if (result.IsFailure)
        {
            ErrorMessage = StockErrorMessages.Describe(result.StockError);
            ShowStocktakeDialog = true;
            View = "stocktakes";
            await LoadAsync(cancellationToken);
            return Page();
        }

        View = "stocktakes";
        return Redirect(PageUrl(includeItem: false));
    }

    private async Task<IActionResult> FailAsync(StockError error, bool openTransfer, string view, CancellationToken cancellationToken)
    {
        ErrorMessage = StockErrorMessages.Describe(error);
        ShowTransferDialog = openTransfer;
        View = view;
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Transfer.Lines is null || Transfer.Lines.Count == 0)
        {
            Transfer.Lines = [new TransferLineForm()];
        }

        var filter = CurrentFilter();
        var filtered = await listHandler.HandleAsync(filter, cancellationToken);
        var badges = await listHandler.HandleAsync(
            new StockListFilter(null, filter.LocationRejected, filter.Category, filter.CategoryRejected, filter.Term),
            cancellationToken);
        Locations = await documentsHandler.ListLocationsAsync(cancellationToken);
        Items = await documentsHandler.ListItemsAsync(cancellationToken);
        Transfers = await documentsHandler.ListTransfersAsync(cancellationToken);
        Stocktakes = await documentsHandler.ListStocktakesAsync(cancellationToken);
        Adjustments = await documentsHandler.ListAdjustmentsAsync(cancellationToken);
        Disposals = await documentsHandler.ListDisposalsAsync(cancellationToken);
        Notices = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId)
            ? await noticesHandler.HandleAsync(employeeId, cancellationToken)
            : [];
        PlaceCodes = BuildPlaceCodes(Locations);
        Groups = BuildGroups(filtered, badges);
        BalanceJson = JsonSerializer.Serialize(
            badges.Select(row => new BalancePoint(row.CatalogItemId, row.StockLocationId, row.OnHand)),
            JsonOptions);
        Detail = null;
        VisibleHistory = [];
        if (Guid.TryParse(Item, out var itemId))
        {
            Detail = await detailHandler.HandleAsync(itemId, cancellationToken);
            SliceHistory();
        }
    }

    private void SliceHistory()
    {
        if (Detail is null)
        {
            return;
        }

        IEnumerable<StockHistoryEntry> rows = Detail.History;
        if (Guid.TryParse(HistoryPlace, out var placeId) && placeId != Guid.Empty)
        {
            rows = rows.Where(entry => entry.StockLocationId == placeId);
        }

        rows = HistoryKind switch
        {
            "receipt" => rows.Where(entry => entry.Kind == StockMovementKind.Receipt),
            "issue" => rows.Where(entry => entry.Kind == StockMovementKind.Issue),
            "transfer" => rows.Where(entry => entry.Kind == StockMovementKind.Transfer),
            "adjustment" => rows.Where(entry => entry.Kind == StockMovementKind.Adjustment),
            _ => rows
        };
        var all = rows.ToArray();
        HistoryTotal = all.Length;
        var size = NormalizedSize();
        var pages = Math.Max(1, (int)Math.Ceiling(HistoryTotal / (double)size));
        var page = HistoryPage < 1 ? 1 : Math.Min(HistoryPage, pages);
        HistoryPage = page;
        HistorySize = size;
        VisibleHistory = all.Skip((page - 1) * size).Take(size).ToArray();
        HistoryFrom = HistoryTotal == 0 ? 0 : ((page - 1) * size) + 1;
        HistoryTo = HistoryTotal == 0 ? 0 : HistoryFrom + VisibleHistory.Count - 1;
    }

    private int NormalizedSize() => HistorySize is 20 or 50 ? HistorySize : 10;

    private static IReadOnlyList<StockItemGroup> BuildGroups(
        IReadOnlyList<StockBalanceRow> filtered,
        IReadOnlyList<StockBalanceRow> badges)
    {
        var byItem = badges.GroupBy(row => row.CatalogItemId).ToDictionary(group => group.Key, group => group.ToArray());
        return filtered
            .GroupBy(row => row.CatalogItemId)
            .Select(group =>
            {
                var first = group.First();
                var places = (byItem.TryGetValue(group.Key, out var all) ? all : group.ToArray())
                    .Where(row => row.StockLocationId != Guid.Empty)
                    .Select(row => new StockPlaceBadge(row.StockLocationId, row.LocationName, row.OnHand))
                    .ToArray();
                return new StockItemGroup(
                    first.CatalogItemId,
                    first.Code,
                    first.Name,
                    first.Brand,
                    first.Origin,
                    first.Unit,
                    first.CostPrice,
                    group.Sum(row => row.OnHand),
                    group.Sum(row => row.Reserved),
                    group.Sum(row => row.Available),
                    group.Sum(row => row.InTransit),
                    places);
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<Guid, string> BuildPlaceCodes(IReadOnlyList<StockLocationChoice> locations)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<Guid, string>();
        foreach (var location in locations.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var code = StockLabels.LocationCode(location.Name, location.Kind);
            var unique = code;
            var suffix = 2;
            while (!used.Add(unique))
            {
                unique = code + suffix;
                suffix++;
            }

            map[location.Id] = unique;
        }

        return map;
    }

    private StockListFilter CurrentFilter() => StockListFilter.From(Location, Category, Search);

    private string? Role() => User.FindFirstValue(ClaimTypes.Role);

    private string PageUrl(bool includeItem) =>
        BuildUrl(ActiveView, includeItem ? Item : null, includeItem ? HistoryKind : null, includeItem ? HistoryPlace : null, includeItem ? HistoryPage : null, includeItem ? HistorySize : null);

    private string BuildUrl(string view, string? item, string? kind, string? place, int? page, int? size)
    {
        var parts = new List<string>();
        if (!string.Equals(view, "balances", StringComparison.Ordinal))
        {
            parts.Add("view=" + Uri.EscapeDataString(view));
        }

        Add(parts, "q", Search);
        Add(parts, "location", Location);
        Add(parts, "category", Category);
        Add(parts, "item", item);
        Add(parts, "kind", kind);
        Add(parts, "place", place);
        if (page is > 1)
        {
            parts.Add("page=" + page.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (size is 20 or 50)
        {
            parts.Add("size=" + size.Value.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/inventory" : "/inventory?" + string.Join('&', parts);
    }

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }

    private sealed record BalancePoint(Guid Item, Guid Location, int OnHand);
}

public sealed class StockItemGroup(
    Guid catalogItemId,
    string code,
    string name,
    string brand,
    string origin,
    string unit,
    decimal costPrice,
    int onHand,
    int reserved,
    int available,
    int inTransit,
    IReadOnlyList<StockPlaceBadge> places)
{
    public Guid CatalogItemId { get; } = catalogItemId;

    public string Code { get; } = code;

    public string Name { get; } = name;

    public string Brand { get; } = brand;

    public string Origin { get; } = origin;

    public string Unit { get; } = unit;

    public decimal CostPrice { get; } = costPrice;

    public int OnHand { get; } = onHand;

    public int Reserved { get; } = reserved;

    public int Available { get; } = available;

    public int InTransit { get; } = inTransit;

    public IReadOnlyList<StockPlaceBadge> Places { get; } = places;
}

public sealed class StockPlaceBadge(Guid locationId, string name, int onHand)
{
    public Guid LocationId { get; } = locationId;

    public string Name { get; } = name;

    public int OnHand { get; } = onHand;
}

public sealed class TransferInput
{
    public Guid SourceLocationId { get; set; }

    public Guid DestinationLocationId { get; set; }

    public string? Note { get; set; }

    public List<TransferLineForm> Lines { get; set; } = [new()];
}

public sealed class TransferLineForm
{
    public Guid CatalogItemId { get; set; }

    public int Quantity { get; set; }
}

public sealed class StocktakeInput
{
    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public int CountedQuantity { get; set; }

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }

    public string? ImageRef { get; set; }
}

public sealed class AdjustmentInput
{
    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public int Delta { get; set; }

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }

    public string? ImageRef { get; set; }
}

public sealed class DisposalInput
{
    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }

    public string? ImageRef { get; set; }
}
