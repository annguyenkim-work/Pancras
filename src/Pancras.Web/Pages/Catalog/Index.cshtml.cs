using Pancras.Web.Security;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Web.Catalog;
using Pancras.Web.Formatting;

namespace Pancras.Web.Pages.Catalog;

[Authorize(Roles = CatalogAccess.ViewRoles)]
public class IndexModel(
    ListCatalogItemsHandler listHandler,
    ListMatchingCatalogItemsHandler exportHandler,
    GetCatalogItemHandler itemHandler,
    CreateCatalogItemCommandHandler createHandler,
    UpdateCatalogPricesCommandHandler priceHandler,
    SetCatalogItemActiveCommandHandler activeHandler,
    CatalogImageStore images,
    IInventoryRepository stock) : PageModel
{
    public CatalogItemPage ItemPage { get; private set; } = new([], 1, CatalogPageRequest.DefaultSize, 0);

    public IReadOnlyList<CatalogItemRow> Rows => ItemPage.Rows;

    public CatalogItemRow? Detail { get; private set; }

    public CatalogError Error { get; private set; }

    public string ErrorTitle { get; private set; } = "Không lưu được mặt hàng";

    public IReadOnlyList<string> ErrorMessages { get; private set; } = [];

    public bool ShowForm { get; private set; }

    public bool ShowDetail { get; private set; }

    public bool ShowPriceForm { get; private set; }

    public bool CostLocked { get; private set; }


    [BindProperty]
    public CatalogItemForm Form { get; set; } = new();

    [BindProperty]
    public CatalogPriceForm Prices { get; set; } = new();

    [BindProperty]
    public IFormFile? Image { get; set; }

    public string? CurrentImagePath { get; private set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "category")]
    public string? CategoryToken { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusToken { get; set; }

    public CatalogStatusFilter StatusFilter => CatalogListFilter.ParseStatus(StatusToken);

    public bool CanDisable => CatalogAccess.CanDisable(User.FindFirstValue(ClaimTypes.Role));

    public int? PageNumber { get; private set; }

    public bool HasFilter
    {
        get
        {
            var filter = CurrentFilter();
            return filter.Term is not null || filter.Category is not null || filter.CategoryRejected || filter.Status != CatalogStatusFilter.Active;
        }
    }

    public string FilteredListUrl => BuildUrl(ItemPage.PageNumber);

    public string PageUrl(int page) => BuildUrl(page);

    public string AddUrl => BuildUrl(ItemPage.PageNumber, add: true);

    public string DetailUrl(Guid id) => BuildUrl(ItemPage.PageNumber, detailId: id);

    public string PriceUrl(Guid id) => BuildUrl(ItemPage.PageNumber, priceId: id);

    public string ExportUrl
    {
        get
        {
            var parts = FilterParts();
            var query = parts.Count == 0 ? string.Empty : "&" + string.Join('&', parts);
            return "/catalog?handler=Export" + query;
        }
    }

    public IReadOnlyList<int> VisiblePages
    {
        get
        {
            const int window = 5;
            var total = ItemPage.TotalPages;
            var current = ItemPage.PageNumber;
            var start = Math.Max(1, current - window / 2);
            var end = Math.Min(total, start + window - 1);
            start = Math.Max(1, end - window + 1);
            return Enumerable.Range(start, end - start + 1).ToArray();
        }
    }

    public async Task<IActionResult> OnGetAsync(Guid? detail, Guid? prices, bool add, CancellationToken cancellationToken)
    {
        ReadPage();
        var requested = CatalogPageRequest.From(PageNumber);
        ItemPage = await listHandler.HandleAsync(CurrentFilter(), requested, cancellationToken);
        if (requested.Number != ItemPage.PageNumber)
        {
            return Redirect(BuildUrl(ItemPage.PageNumber, detail, prices, add));
        }

        if (add)
        {
            ShowForm = true;
            ApplyAddDefaults();
            return Page();
        }

        if (prices is Guid priceId)
        {
            return await OpenPricesAsync(priceId, cancellationToken);
        }

        if (detail is Guid detailId)
        {
            return await OpenDetailAsync(detailId, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        var rows = await exportHandler.HandleAsync(CurrentFilter(), cancellationToken);
        return File(CatalogCsv.Write(rows), "text/csv; charset=utf-8", "catalog.csv");
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        var image = await images.ReadAsync(Image, cancellationToken);
        var parsed = CatalogFormReader.TryReadCreate(Form, out var command, out var parseIssues);
        if (!image.Accepted || !parsed)
        {
            RememberCombined("Không thêm được mặt hàng", image, parsed, parseIssues, Form.Code);
            ShowForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        if (image.HasContent)
        {
            command.ImagePath = await images.SaveAsync(image, cancellationToken);
        }

        var result = await createHandler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            images.Delete(command.ImagePath);
            RememberFailure("Không thêm được mặt hàng", result.CatalogError, result.CatalogIssues, Form.Code);
            ShowForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var created = result.Value!;
        TempData.Toast("Đã thêm mặt hàng.");
        CategoryToken = ProductCategoryToken.From(created.Category);
        Search = created.Code;
        return Redirect(BuildUrl(1));
    }

    public async Task<IActionResult> OnPostSetActiveAsync(Guid id, bool active, int version, CancellationToken cancellationToken)
    {
        ReadPage();
        var actor = User.ToActor();
        var error = await activeHandler.HandleAsync(id, active, version, actor, cancellationToken);
        if (error == CatalogError.None)
        {
            TempData.Toast(active ? "Đã Active mặt hàng." : "Đã Disable mặt hàng. Mặt hàng không còn hiện trong danh sách chọn.");
        }
        else
        {
            TempData.Toast(string.Join(" ", CatalogErrorMessages.Describe(error, [], null)), ToastLevel.Error);
        }

        return Redirect(BuildUrl(PageNumber ?? 1));
    }

    public async Task<IActionResult> OnPostUpdatePricesAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var image = await images.ReadAsync(Image, cancellationToken);
        var parsed = CatalogFormReader.TryReadPrices(Prices, role, out var command, out var parseIssues);
        if (!image.Accepted || !parsed)
        {
            CurrentImagePath = await ExistingImagePathAsync(Prices.Id, cancellationToken);
            RememberCombined("Không lưu được sản phẩm", image, parsed, parseIssues, Prices.Code);
            await RefreshCostLockAsync(Prices.Id, cancellationToken);
            ShowPriceForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var previousImage = await ExistingImagePathAsync(Prices.Id, cancellationToken);
        if (image.HasContent)
        {
            command.ImagePath = await images.SaveAsync(image, cancellationToken);
            command.ReplaceImage = true;
        }

        var result = await priceHandler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            images.Delete(command.ImagePath);
            CurrentImagePath = previousImage;
            RememberFailure("Không lưu được sản phẩm", result.CatalogError, result.CatalogIssues, Prices.Code);
            await RefreshCostLockAsync(Prices.Id, cancellationToken);
            ShowPriceForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        if (command.ReplaceImage)
        {
            images.Delete(previousImage);
        }

        TempData.Toast("Đã lưu sản phẩm.");
        return Redirect(BuildUrl(CatalogPageRequest.From(PageNumber).Number));
    }

    private async Task<IActionResult> OpenDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await itemHandler.HandleAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không mở được mặt hàng", result.CatalogError, result.CatalogIssues, null);
            return Page();
        }

        Detail = result.Value;
        ShowDetail = true;
        return Page();
    }

    private async Task<IActionResult> OpenPricesAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await itemHandler.HandleAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không mở được bảng giá", result.CatalogError, result.CatalogIssues, null);
            return Page();
        }

        var row = result.Value!;
        Prices = new CatalogPriceForm
        {
            Id = row.Id,
            IncludeDetails = true,
            Code = row.Code,
            Name = row.Name,
            Category = ProductCategoryToken.From(row.Category),
            Brand = row.Brand,
            Origin = row.Origin,
            Unit = row.Unit,
            WeightKg = MeasureText(row.WeightKg),
            VolumeM3 = MeasureText(row.VolumeM3),
            WarrantyMonths = row.WarrantyMonths?.ToString(CultureInfo.InvariantCulture),
            MinimumStock = MeasureText(row.MinimumStock),
            HeightMm = MillimeterText(row.HeightMm),
            LengthMm = MillimeterText(row.LengthMm),
            WidthMm = MillimeterText(row.WidthMm),
            CostPrice = VndMoney.FormatDong(row.CostPrice),
            RetailPrice = VndMoney.FormatDong(row.RetailPrice),
            DealerPrice = VndMoney.FormatDong(row.DealerPrice),
            DistributorPrice = VndMoney.FormatDong(row.DistributorPrice),
            ProjectPrice = VndMoney.FormatDong(row.ProjectPrice),
            Version = row.Version
        };
        await RefreshCostLockAsync(row.Id, cancellationToken);
        CurrentImagePath = row.ImagePath;
        ShowPriceForm = true;
        return Page();
    }

    private void ApplyAddDefaults()
    {
        Form.Category = string.IsNullOrWhiteSpace(Form.Category) ? ProductCategoryToken.Sanitary : Form.Category;
        Form.Unit = string.IsNullOrWhiteSpace(Form.Unit) ? "Cái" : Form.Unit;
        Form.Origin = string.IsNullOrWhiteSpace(Form.Origin) ? "Nhập khẩu" : Form.Origin;
        Form.WarrantyMonths = string.IsNullOrWhiteSpace(Form.WarrantyMonths) ? "24" : Form.WarrantyMonths;
        Form.WeightKg = string.IsNullOrWhiteSpace(Form.WeightKg) ? "10" : Form.WeightKg;
        Form.MinimumStock = string.IsNullOrWhiteSpace(Form.MinimumStock) ? "5" : Form.MinimumStock;
        Form.CostPrice = string.IsNullOrWhiteSpace(Form.CostPrice) ? "0 VNĐ" : Form.CostPrice;
        Form.RetailPrice = string.IsNullOrWhiteSpace(Form.RetailPrice) ? "0 VNĐ" : Form.RetailPrice;
        Form.DealerPrice = string.IsNullOrWhiteSpace(Form.DealerPrice) ? "0 VNĐ" : Form.DealerPrice;
        Form.DistributorPrice = string.IsNullOrWhiteSpace(Form.DistributorPrice) ? "0 VNĐ" : Form.DistributorPrice;
        Form.ProjectPrice = string.IsNullOrWhiteSpace(Form.ProjectPrice) ? "0 VNĐ" : Form.ProjectPrice;
    }

    private async Task RefreshCostLockAsync(Guid id, CancellationToken cancellationToken)
    {
        CostLocked = id != Guid.Empty && await stock.SumOnHandAsync(id, cancellationToken) > 0;
    }

    private static string MeasureText(decimal? amount) =>
        amount is null ? string.Empty : VndMoney.FormatMeasure(amount.Value);

    private static string MillimeterText(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private async Task<string?> ExistingImagePathAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await itemHandler.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? result.Value?.ImagePath : null;
    }

    private void RememberCombined(
        string title,
        CatalogImageDraft image,
        bool parsed,
        IReadOnlyList<CatalogIssue> issues,
        string? code)
    {
        var messages = new List<string>();
        if (!image.Accepted && image.Error is not null)
        {
            messages.Add(image.Error);
        }

        if (!parsed)
        {
            messages.AddRange(CatalogErrorMessages.Describe(CatalogError.Incomplete, issues, code));
        }

        ErrorTitle = title;
        Error = parsed ? CatalogError.None : CatalogError.Incomplete;
        ErrorMessages = messages;
    }

    private CatalogListFilter CurrentFilter() => CatalogListFilter.From(CategoryToken, Search, StatusToken);

    private void ReadPage()
    {
        PageNumber = null;
        if (Request.HasFormContentType
            && Request.Form.TryGetValue("page", out var posted)
            && int.TryParse(posted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromForm))
        {
            PageNumber = fromForm;
            return;
        }

        if (Request.Query.TryGetValue("page", out var query)
            && int.TryParse(query, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromQuery))
        {
            PageNumber = fromQuery;
        }
    }

    private void RememberFailure(
        string title,
        CatalogError error,
        IReadOnlyList<CatalogIssue> issues,
        string? code)
    {
        Error = error;
        ErrorTitle = title;
        ErrorMessages = CatalogErrorMessages.Describe(error, issues, code);
    }

    private string BuildUrl(int page, Guid? detailId = null, Guid? priceId = null, bool add = false)
    {
        var parts = new List<string>();
        if (detailId is Guid detail)
        {
            parts.Add("detail=" + detail.ToString("D"));
        }

        if (priceId is Guid price)
        {
            parts.Add("prices=" + price.ToString("D"));
        }

        if (add)
        {
            parts.Add("add=true");
        }

        parts.AddRange(FilterParts());
        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/catalog" : "/catalog?" + string.Join('&', parts);
    }

    private List<string> FilterParts()
    {
        var filter = CurrentFilter();
        var parts = new List<string>();
        if (filter.Category is ProductCategory category)
        {
            parts.Add("category=" + ProductCategoryToken.From(category));
        }
        else if (filter.CategoryRejected && !string.IsNullOrWhiteSpace(CategoryToken))
        {
            parts.Add("category=" + Uri.EscapeDataString(CategoryToken.Trim()));
        }

        if (filter.Term is not null)
        {
            parts.Add("q=" + Uri.EscapeDataString(filter.Term));
        }

        if (filter.Status != CatalogStatusFilter.Active)
        {
            parts.Add("status=" + filter.Status.ToString().ToLowerInvariant());
        }

        return parts;
    }
}
