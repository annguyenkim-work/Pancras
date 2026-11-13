using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;
using Pancras.Web.Security;
using Pancras.Web.Formatting;

namespace Pancras.Web.Pages.Inventory;

[Authorize(Roles = InventoryAccess.ViewRoles)]
public class IndexModel(
    ListStockBalancesHandler listHandler,
    ListInventoryDocumentsHandler documentsHandler,
    GetStockItemDetailHandler detailHandler,
    CreateTransferRequestCommandHandler requestHandler,
    DecideTransferCommandHandler decideHandler,
    ShipTransferCommandHandler shipHandler,
    ResolveTransferDiscrepancyCommandHandler resolveHandler,
    ReturnTransferCommandHandler returnHandler,
    UpdateTransferRequestCommandHandler updateRequestHandler,
    UploadTransferAttachmentCommandHandler uploadTransferAttachment,
    OpenTransferAttachmentHandler openTransferAttachment,
    CancelTransferCommandHandler cancelTransferHandler,
    ConfirmTransferReceiptCommandHandler receiptHandler,
    CreateStocktakeCommandHandler createStocktakeHandler,
    ApproveStocktakeCommandHandler approveStocktakeHandler,
    CreateQuantityAdjustmentCommandHandler createAdjustmentHandler,
    ApproveQuantityAdjustmentCommandHandler approveAdjustmentHandler,
    CreateDisposalSlipCommandHandler createDisposalHandler,
    ApproveDisposalSlipCommandHandler approveDisposalHandler,
    ListStockNoticesHandler noticesHandler,
    UploadStockDocumentAttachmentCommandHandler uploadStockDocumentFile,
    OpenStockDocumentAttachmentHandler openStockDocumentFile) : PageModel
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

    public Pancras.Domain.Employees.EmployeeActor Actor => User.ToActor();

    public IReadOnlyList<StockLocationChoice> AllLocations { get; private set; } = [];

    public bool CanOperate => InventoryAccess.CanOperate(Role());

    public bool CanSeeCost => InventoryAccess.CanSeeCost(Role());

    public bool CanExport => InventoryAccess.CanExport(Role());

    public bool IsGlobal => InventoryAccess.IsGlobal(Role());

    public bool CanCountStock => InventoryAccess.CanOperate(Role());

    public bool CanApproveStocktake => InventoryRoles.IsShowroomLead(Role()) || InventoryRoles.IsFinance(Role());

    public bool CanDraftAdjustment => InventoryRoles.CanDraftAdjustment(Role());

    public bool CanPostAdjustment => InventoryRoles.CanPostAdjustment(Role());

    public bool CanApproveDisposal => InventoryRoles.IsShowroomLead(Role()) || InventoryRoles.IsFinance(Role());

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

    public const int TransferPageSize = 10;

    /// <summary>Số phiếu đang chờ chính người dùng xử lý (duyệt, xuất, nhận hoặc xử lý chênh lệch), hiện trên tab Chuyển kho.</summary>
    public int PendingForMeCount => Transfers.Count(slip => Waiting(slip, "all-mine"));

    public int CountFor(string quick) => Transfers.Count(slip => Waiting(slip, quick));

    public IReadOnlyList<TransferSlipRow> FilteredTransfers => Transfers.Where(MatchesTransferFilter).ToArray();

    public int TransferTotal => FilteredTransfers.Count;

    public int TransferPageCount => Math.Max(1, (int)Math.Ceiling(TransferTotal / (double)TransferPageSize));

    public int CurrentTransferPage => Math.Clamp(TransferPage, 1, TransferPageCount);

    /// <summary>Phiếu của trang hiện tại, kèm phiếu đang được mở (sau thao tác lỗi hoặc mở từ thông báo) dù nó nằm ở trang khác.</summary>
    public IReadOnlyList<TransferSlipRow> PagedTransfers
    {
        get
        {
            var page = FilteredTransfers.Skip((CurrentTransferPage - 1) * TransferPageSize).Take(TransferPageSize).ToList();
            if (OpenTransferId is Guid open && page.All(slip => slip.Id != open) && Transfers.FirstOrDefault(slip => slip.Id == open) is { } extra)
            {
                page.Add(extra);
            }

            return page;
        }
    }

    public bool HasTransferFilter => !string.IsNullOrWhiteSpace(TransferQuick)
        || !string.IsNullOrWhiteSpace(TransferStatusFilter)
        || !string.IsNullOrWhiteSpace(TransferLocation)
        || !string.IsNullOrWhiteSpace(TransferSearch)
        || !string.IsNullOrWhiteSpace(TransferFrom)
        || !string.IsNullOrWhiteSpace(TransferTo);

    /// <summary>Phiếu đang chờ người dùng theo từng nhóm: decide (duyệt), ship (xuất), receive (nhận), resolve (xử lý chênh lệch), all-mine (bất kỳ nhóm nào).</summary>
    private static bool Waiting(TransferSlipRow slip, string quick) => slip.Flow is { } flow && quick switch
    {
        "decide" => flow.CanDecide,
        "ship" => flow.CanShip,
        "receive" => flow.CanReceive,
        "resolve" => flow.CanResolve,
        "all-mine" => flow.CanDecide || flow.CanShip || flow.CanReceive || flow.CanResolve,
        _ => false
    };

    private bool MatchesTransferFilter(TransferSlipRow slip)
    {
        if (!string.IsNullOrWhiteSpace(TransferQuick) && TransferQuick != "all" && !Waiting(slip, TransferQuick))
        {
            return false;
        }

        if (TransferStatusFilter is { Length: > 0 } token
            && Enum.TryParse<TransferStatus>(token, ignoreCase: true, out var status)
            && slip.Status != status)
        {
            return false;
        }

        if (Guid.TryParse(TransferLocation, out var place) && slip.SourceId != place && slip.DestinationId != place)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(TransferSearch))
        {
            var term = TransferSearch.Trim();
            var code = StockLabels.DocumentCode("CK", slip.Id);
            if (!code.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !slip.Lines.Any(line => line.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || line.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        var created = StockLabels.Version7Time(slip.Id)?.ToLocalTime().Date;
        if (created is DateTime day)
        {
            if (DateTime.TryParse(TransferFrom, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) && day < from.Date)
            {
                return false;
            }

            if (DateTime.TryParse(TransferTo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) && day > to.Date)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Số thực nhận đã nhập, giữ lại khi nhận hàng bị từ chối để người dùng không phải nhập lại.</summary>
    public IReadOnlyDictionary<Guid, int> EchoReceived { get; private set; } = new Dictionary<Guid, int>();

    public string? EchoReceiveNote { get; private set; }

    public Guid? OpenTransferId { get; private set; }

    public string ActiveView => !CanOperate ? "balances" : View switch
    {
        "transfers" => "transfers",
        "stocktakes" => "stocktakes",
        _ => "balances"
    };

    public string Subtitle
    {
        get
        {
            var warehouses = AllLocations.Where(location => location.Kind == StockLocationKind.Warehouse).Select(location => location.Name).ToArray();
            var showrooms = AllLocations.Where(location => location.Kind == StockLocationKind.Showroom).Select(location => location.Name).ToArray();
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

    public string LocationOptionLabel => AllLocations.Count == 0
        ? "Toàn hệ thống"
        : $"Toàn hệ thống ({AllLocations.Count} địa điểm)";

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

    [BindProperty(SupportsGet = true, Name = "tf")]
    public string? TransferQuick { get; set; }

    [BindProperty(SupportsGet = true, Name = "ts")]
    public string? TransferStatusFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "tl")]
    public string? TransferLocation { get; set; }

    [BindProperty(SupportsGet = true, Name = "tq")]
    public string? TransferSearch { get; set; }

    [BindProperty(SupportsGet = true, Name = "tfrom")]
    public string? TransferFrom { get; set; }

    [BindProperty(SupportsGet = true, Name = "tto")]
    public string? TransferTo { get; set; }

    [BindProperty(SupportsGet = true, Name = "tp")]
    public int TransferPage { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "open")]
    public Guid? OpenTransfer { get; set; }

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
        var rows = await listHandler.ExportAsync(CurrentFilter(), Actor, cancellationToken);
        return File(StockCsv.Write(rows), "text/csv; charset=utf-8", "ton-kho.csv");
    }

    /// <summary>Bước 1: lập phiếu đề nghị chuyển kho (chờ duyệt, chưa trừ tồn).</summary>
    public async Task<IActionResult> OnPostRequestTransferAsync(CancellationToken cancellationToken)
    {
        var lines = (Transfer.Lines ?? [])
            .Where(line => line.CatalogItemId != Guid.Empty && line.Quantity > 0)
            .Select(line => new TransferLineInput(line.CatalogItemId, line.Quantity))
            .ToArray();
        var created = await requestHandler.HandleAsync(
            new CreateTransferSlipCommand
            {
                SourceLocationId = Transfer.SourceLocationId,
                DestinationLocationId = Transfer.DestinationLocationId,
                Note = Transfer.Note,
                Lines = lines,
                Actor = Actor
            },
            cancellationToken);
        if (created.IsFailure || created.Value == Guid.Empty)
        {
            return await FailAsync(created.StockError, created.Detail, openTransfer: true, "transfers", cancellationToken);
        }

        View = "transfers";
        return Redirect(PageUrl(includeItem: false));
    }

    /// <summary>Sửa phiếu đề nghị khi chưa được duyệt.</summary>
    public async Task<IActionResult> OnPostUpdateTransferAsync(CancellationToken cancellationToken)
    {
        var lines = (Transfer.Lines ?? [])
            .Where(line => line.CatalogItemId != Guid.Empty && line.Quantity > 0)
            .Select(line => new TransferLineInput(line.CatalogItemId, line.Quantity))
            .ToArray();
        return await SaveAsync(
            updateRequestHandler.HandleAsync(
                new UpdateTransferRequestCommand
                {
                    TransferSlipId = DocumentId,
                    SourceLocationId = Transfer.SourceLocationId,
                    DestinationLocationId = Transfer.DestinationLocationId,
                    Note = Transfer.Note,
                    Lines = lines,
                    Actor = Actor
                },
                cancellationToken),
            "transfers",
            cancellationToken);
    }

    public Task<IActionResult> OnPostDecideTransferAsync(bool approve, string? reason, CancellationToken cancellationToken) =>
        SaveAsync(decideHandler.HandleAsync(new DecideTransferCommand(DocumentId, Actor, approve, reason), cancellationToken), "transfers", cancellationToken);

    public Task<IActionResult> OnPostShipTransferAsync(string? sealNumber, string? carrier, CancellationToken cancellationToken) =>
        SaveAsync(shipHandler.HandleAsync(new ShipTransferCommand(DocumentId, Actor, sealNumber, carrier), cancellationToken), "transfers", cancellationToken);

    /// <summary>Hủy phiếu chưa xuất kho; chỉ người lập, bắt buộc lý do.</summary>
    public Task<IActionResult> OnPostCancelTransferAsync(string? reason, CancellationToken cancellationToken) =>
        SaveAsync(cancelTransferHandler.HandleAsync(new ConfirmTransferCommand(DocumentId, Actor, null, reason), cancellationToken), "transfers", cancellationToken);

    /// <summary>Trả hàng đang vận chuyển về kho xuất; chỉ Admin, Giám đốc, Kế toán, bắt buộc lý do.</summary>
    public Task<IActionResult> OnPostReturnTransferAsync(string? reason, CancellationToken cancellationToken) =>
        SaveAsync(returnHandler.HandleAsync(new ReturnTransferCommand(DocumentId, Actor, reason), cancellationToken), "transfers", cancellationToken);

    /// <summary>Kiểm nhận: số thực nhận từng dòng (bỏ trống = nhận đủ). Khác số xuất thì cần lý do và file đính kèm.</summary>
    public async Task<IActionResult> OnPostReceiveTransferAsync(
        List<ReceiveLineForm>? receiveLines,
        string? receiveNote,
        List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var actuals = (receiveLines ?? [])
            .Where(line => line.Quantity is not null)
            .Select(line => new ReceivedLineInput(line.LineId, line.Quantity!.Value))
            .ToArray();

        EchoReceived = (receiveLines ?? []).Where(line => line.Quantity is not null).ToDictionary(line => line.LineId, line => line.Quantity!.Value);
        EchoReceiveNote = receiveNote;

        // Kiểm tra phiếu nhận trước; chỉ lưu file khi phiếu hợp lệ để không để lại file mồ côi sau lần nhận bị từ chối.
        var checkedReceipt = await receiptHandler.CheckAsync(
            new ConfirmTransferCommand(DocumentId, Actor, actuals, receiveNote),
            (files ?? []).Count(file => file.Length > 0),
            cancellationToken);
        if (checkedReceipt.IsFailure)
        {
            return await FailAsync(checkedReceipt.StockError, checkedReceipt.Detail, openTransfer: false, "transfers", cancellationToken);
        }

        if (await UploadTransferFilesAsync(files, cancellationToken) is { } uploadFailure)
        {
            return uploadFailure;
        }

        return await SaveAsync(receiptHandler.HandleAsync(new ConfirmTransferCommand(DocumentId, Actor, actuals, receiveNote), cancellationToken), "transfers", cancellationToken);
    }

    public Task<IActionResult> OnPostResolveTransferAsync(string? resolution, CancellationToken cancellationToken) =>
        SaveAsync(resolveHandler.HandleAsync(new ResolveTransferCommand(DocumentId, Actor, resolution), cancellationToken), "transfers", cancellationToken);

    public async Task<IActionResult> OnPostAttachTransferFileAsync(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        if (await UploadTransferFilesAsync(files, cancellationToken) is { } uploadFailure)
        {
            return uploadFailure;
        }

        View = "transfers";
        return Redirect(PageUrl(includeItem: false));
    }

    /// <summary>Lưu các file đính kèm cho phiếu hiện tại; trả về kết quả lỗi nếu một file bị từ chối, null nếu xong.</summary>
    private async Task<IActionResult?> UploadTransferFilesAsync(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        var chosen = (files ?? []).Where(file => file.Length > 0).ToArray();
        var pending = new List<PendingTransferFile>();
        foreach (var file in chosen)
        {
            var header = new byte[8];
            await using var probe = file.OpenReadStream();
            var read = await probe.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
            pending.Add(new PendingTransferFile(file.FileName, file.Length, header[..read]));
        }

        // Kiểm tra cả loạt trước, rồi mới lưu: một file sai không để lại các file đứng trước nó.
        var verdict = await uploadTransferAttachment.CheckAsync(DocumentId, pending, Actor, cancellationToken);
        if (verdict.IsFailure)
        {
            return await FailAsync(verdict.StockError, verdict.Detail, openTransfer: false, "transfers", cancellationToken);
        }

        foreach (var file in chosen)
        {
            await using var content = file.OpenReadStream();
            var uploaded = await uploadTransferAttachment.HandleAsync(
                new UploadTransferAttachmentCommand { TransferSlipId = DocumentId, FileName = file.FileName, Content = content, Actor = Actor },
                cancellationToken);
            if (uploaded.IsFailure)
            {
                return await FailAsync(uploaded.StockError, uploaded.Detail, openTransfer: false, "transfers", cancellationToken);
            }
        }

        return null;
    }

    public async Task<IActionResult> OnGetTransferFileAsync(Guid id)
    {
        var result = await openTransferAttachment.HandleAsync(id, Actor, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        return File(download.Content, download.ContentType, download.FileName);
    }

    public Task<IActionResult> OnPostCreateStocktakeAsync(List<IFormFile>? files, CancellationToken cancellationToken) =>
        CreateStockDocumentAsync(
            StockDocumentKind.Stocktake,
            () => createStocktakeHandler.HandleAsync(
                new CreateStocktakeCommand
                {
                    Actor = Actor,
                    StockLocationId = Stocktake.StockLocationId,
                    CatalogItemId = Stocktake.CatalogItemId,
                    CountedQuantity = Stocktake.CountedQuantity,
                    Reason = Stocktake.Reason,
                    DocumentRef = Stocktake.DocumentRef
                },
                cancellationToken),
            files,
            cancellationToken);

    public Task<IActionResult> OnPostApproveStocktakeAsync(CancellationToken cancellationToken) =>
        SaveAsync(approveStocktakeHandler.HandleAsync(new ApproveDocumentCommand(DocumentId, Actor), cancellationToken), "stocktakes", cancellationToken);

    public Task<IActionResult> OnPostCreateAdjustmentAsync(List<IFormFile>? files, CancellationToken cancellationToken) =>
        CreateStockDocumentAsync(
            StockDocumentKind.Adjustment,
            () => createAdjustmentHandler.HandleAsync(
                new CreateQuantityAdjustmentCommand
                {
                    Actor = Actor,
                    StockLocationId = Adjustment.StockLocationId,
                    CatalogItemId = Adjustment.CatalogItemId,
                    Delta = Adjustment.Delta,
                    Reason = Adjustment.Reason,
                    DocumentRef = Adjustment.DocumentRef
                },
                cancellationToken),
            files,
            cancellationToken);

    public Task<IActionResult> OnPostApproveAdjustmentAsync(CancellationToken cancellationToken) =>
        SaveAsync(approveAdjustmentHandler.HandleAsync(new ApproveDocumentCommand(DocumentId, Actor), cancellationToken), "stocktakes", cancellationToken);

    public Task<IActionResult> OnPostCreateDisposalAsync(List<IFormFile>? files, CancellationToken cancellationToken) =>
        CreateStockDocumentAsync(
            StockDocumentKind.Disposal,
            () => createDisposalHandler.HandleAsync(
                new CreateDisposalSlipCommand
                {
                    Actor = Actor,
                    StockLocationId = Disposal.StockLocationId,
                    CatalogItemId = Disposal.CatalogItemId,
                    Reason = Disposal.Reason,
                    DocumentRef = Disposal.DocumentRef
                },
                cancellationToken),
            files,
            cancellationToken);

    public async Task<IActionResult> OnPostOpenNoticeAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        StockNotice? notice = null;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId))
        {
            notice = await noticesHandler.OpenAsync(noticeId, employeeId, cancellationToken);
        }

        var target = notice switch
        {
            null => "/inventory?view=transfers",
            { Kind: StockNoticeKind.Transfer } => $"/inventory?view=transfers&open={notice.DocumentId:D}",
            _ => $"/inventory?view=stocktakes#document-{notice.DocumentId:D}"
        };

        return Redirect(target);
    }

    public Task<IActionResult> OnPostApproveDisposalAsync(CancellationToken cancellationToken) =>
        SaveAsync(approveDisposalHandler.HandleAsync(new ApproveDocumentCommand(DocumentId, Actor), cancellationToken), "stocktakes", cancellationToken);

    /// <summary>Địa chỉ trang Chuyển kho với bộ lọc hiện tại, đổi một vài tham số (null thì bỏ tham số đó).</summary>
    public string TransferUrl(string? quick = null, int? page = null, bool keepFilters = true)
    {
        var parts = new List<string> { "view=transfers" };
        if (keepFilters)
        {
            Add(parts, "ts", TransferStatusFilter);
            Add(parts, "tl", TransferLocation);
            Add(parts, "tq", TransferSearch);
            Add(parts, "tfrom", TransferFrom);
            Add(parts, "tto", TransferTo);
        }

        if (!string.IsNullOrEmpty(quick) && quick != "all")
        {
            parts.Add("tf=" + Uri.EscapeDataString(quick));
        }

        if (page is > 1)
        {
            parts.Add("tp=" + page.Value.ToString(CultureInfo.InvariantCulture));
        }

        return "/inventory?" + string.Join('&', parts);
    }

    public string ViewUrl(string view) =>
 BuildUrl(view, item: null, kind: null, place: null, page: null, size: null);

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
            return await FailAsync(result.StockError, result.Detail, openTransfer: false, view, cancellationToken);
        }

        View = view;
        if (view == "transfers" && DocumentId != Guid.Empty)
        {
            // Mở lại phiếu vừa thao tác để người dùng thấy trạng thái mới và lịch sử.
            return Redirect(TransferUrl(keepFilters: false) + "&open=" + DocumentId.ToString("D"));
        }

        return Redirect(PageUrl(includeItem: false));
    }

    /// <summary>
    /// Lập biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy kèm file đính kèm (ảnh hàng, chứng từ).
    /// File được kiểm tra trước khi tạo phiếu để một file sai không để lại phiếu nửa vời.
    /// </summary>
    private async Task<IActionResult> CreateStockDocumentAsync(
        StockDocumentKind kind,
        Func<Task<Result<Guid>>> create,
        List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var chosen = (files ?? []).Where(file => file.Length > 0).ToArray();
        var pending = new List<PendingTransferFile>();
        foreach (var file in chosen)
        {
            var header = new byte[8];
            await using var probe = file.OpenReadStream();
            var read = await probe.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
            pending.Add(new PendingTransferFile(file.FileName, file.Length, header[..read]));
        }

        var verdict = UploadStockDocumentAttachmentCommandHandler.CheckFiles(pending);
        if (verdict.IsFailure)
        {
            return await FailStockDocumentAsync(kind, verdict.StockError, verdict.Detail, cancellationToken);
        }

        var created = await create();
        if (created.IsFailure)
        {
            return await FailStockDocumentAsync(kind, created.StockError, created.Detail, cancellationToken);
        }

        foreach (var file in chosen)
        {
            await using var content = file.OpenReadStream();
            var uploaded = await uploadStockDocumentFile.HandleAsync(
                new UploadStockDocumentAttachmentCommand { Kind = kind, DocumentId = created.Value, FileName = file.FileName, Content = content, Actor = Actor },
                cancellationToken);
            if (uploaded.IsFailure)
            {
                return await FailStockDocumentAsync(kind, uploaded.StockError, uploaded.Detail, cancellationToken);
            }
        }

        View = "stocktakes";
        return Redirect(PageUrl(includeItem: false));
    }

    private async Task<IActionResult> FailStockDocumentAsync(StockDocumentKind kind, StockError error, string? detail, CancellationToken cancellationToken)
    {
        ErrorMessage = StockErrorMessages.Describe(error, detail);
        ShowStocktakeDialog = kind == StockDocumentKind.Stocktake;
        View = "stocktakes";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetStockFileAsync(Guid id)
    {
        var result = await openStockDocumentFile.HandleAsync(id, Actor, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        return File(download.Content, download.ContentType, download.FileName);
    }

    private async Task<IActionResult> FailAsync(StockError error, string? detail, bool openTransfer, string view, CancellationToken cancellationToken)
    {
        ErrorMessage = StockErrorMessages.Describe(error, detail);
        OpenTransfer = DocumentId == Guid.Empty ? null : DocumentId;
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
        var filtered = await listHandler.HandleAsync(filter, Actor, cancellationToken);
        var badges = await listHandler.HandleAsync(
            new StockListFilter(null, filter.LocationRejected, filter.Category, filter.CategoryRejected, filter.Term),
            Actor,
            cancellationToken);
        Locations = await documentsHandler.ListLocationsAsync(Actor, cancellationToken);
        AllLocations = await documentsHandler.ListAllLocationsAsync(cancellationToken);
        Items = await documentsHandler.ListItemsAsync(cancellationToken);
        Transfers = await documentsHandler.ListTransfersAsync(Actor, cancellationToken);
        Stocktakes = await documentsHandler.ListStocktakesAsync(Actor, cancellationToken);
        Adjustments = await documentsHandler.ListAdjustmentsAsync(Actor, cancellationToken);
        Disposals = await documentsHandler.ListDisposalsAsync(Actor, cancellationToken);
        OpenTransferId = OpenTransfer;
        // Chưa xem lên trước, mới nhất trước; chỉ hiện một số thông báo để banner không dài vô hạn.
        Notices = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId)
            ? (await noticesHandler.HandleAsync(employeeId, cancellationToken)).OrderBy(notice => notice.Seen).ThenByDescending(notice => notice.CreatedAt).Take(8).ToArray()
            : [];
        PlaceCodes = BuildPlaceCodes(AllLocations);
        Groups = BuildGroups(filtered, badges);
        BalanceJson = JsonSerializer.Serialize(
            badges.Select(row => new BalancePoint(row.CatalogItemId, row.StockLocationId, row.OnHand, row.Available)),
            JsonOptions);
        Detail = null;
        VisibleHistory = [];
        if (Guid.TryParse(Item, out var itemId))
        {
            Detail = await detailHandler.HandleAsync(itemId, Actor, cancellationToken);
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

    private sealed record BalancePoint(Guid Item, Guid Location, int OnHand, int Available);
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
}

public sealed class AdjustmentInput
{
    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public int Delta { get; set; }

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }
}

public sealed class DisposalInput
{
    public Guid StockLocationId { get; set; }

    public Guid CatalogItemId { get; set; }

    public string? Reason { get; set; }

    public string? DocumentRef { get; set; }
}

public sealed class ReceiveLineForm
{
    public Guid LineId { get; set; }

    public int? Quantity { get; set; }
}
