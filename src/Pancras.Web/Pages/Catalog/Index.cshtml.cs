using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;
using Pancras.Web.Formatting;

namespace Pancras.Web.Pages.Catalog;

[Authorize(Roles = CatalogAccess.ViewRoles)]
public class IndexModel(
    ListCatalogItemsHandler listHandler,
    ListMatchingCatalogItemsHandler exportHandler,
    GetCatalogItemHandler itemHandler,
    CreateCatalogItemCommandHandler createHandler,
    UpdateCatalogPricesCommandHandler priceHandler) : PageModel
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

    public string? Notice { get; private set; }

    [BindProperty]
    public CatalogItemForm Form { get; set; } = new();

    [BindProperty]
    public CatalogPriceForm Prices { get; set; } = new();

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "nganh")]
    public string? CategoryToken { get; set; }

    public int? PageNumber { get; private set; }

    public bool HasFilter
    {
        get
        {
            var filter = CurrentFilter();
            return filter.Term is not null || filter.Category is not null || filter.CategoryRejected;
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
            return "/danh-muc-gia?handler=Export" + query;
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

    public async Task<IActionResult> OnGetAsync(Guid? xem, Guid? gia, bool them, CancellationToken cancellationToken)
    {
        ReadPage();
        var requested = CatalogPageRequest.From(PageNumber);
        ItemPage = await listHandler.HandleAsync(CurrentFilter(), requested, cancellationToken);
        if (requested.Number != ItemPage.PageNumber)
        {
            return Redirect(BuildUrl(ItemPage.PageNumber, xem, gia, them));
        }

        Notice = TempData["CatalogNotice"] as string;
        if (them)
        {
            ShowForm = true;
            ApplyAddDefaults();
            return Page();
        }

        if (gia is Guid priceId)
        {
            return await OpenPricesAsync(priceId, cancellationToken);
        }

        if (xem is Guid detailId)
        {
            return await OpenDetailAsync(detailId, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        var rows = await exportHandler.HandleAsync(CurrentFilter(), cancellationToken);
        return File(CatalogCsv.Write(rows), "text/csv; charset=utf-8", "danh-muc-gia.csv");
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        if (!CatalogFormReader.TryReadCreate(Form, out var command, out var parseIssues))
        {
            RememberFailure("Không thêm được mặt hàng", CatalogError.Incomplete, parseIssues, Form.Code);
            ShowForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var result = await createHandler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không thêm được mặt hàng", result.CatalogError, result.CatalogIssues, Form.Code);
            ShowForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var created = result.Value!;
        TempData["CatalogNotice"] = "Đã thêm mặt hàng.";
        CategoryToken = ProductCategoryToken.From(created.Category);
        Search = created.Code;
        return Redirect(BuildUrl(1));
    }

    public async Task<IActionResult> OnPostUpdatePricesAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (!CatalogFormReader.TryReadPrices(Prices, role, out var command, out var parseIssues))
        {
            RememberFailure("Không lưu được sản phẩm", CatalogError.Incomplete, parseIssues, Prices.Code);
            ShowPriceForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var result = await priceHandler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không lưu được sản phẩm", result.CatalogError, result.CatalogIssues, Prices.Code);
            ShowPriceForm = true;
            ItemPage = await listHandler.HandleAsync(CurrentFilter(), CatalogPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        TempData["CatalogNotice"] = "Đã lưu sản phẩm.";
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
            CostPrice = VndMoney.FormatDong(row.CostPrice),
            RetailPrice = VndMoney.FormatDong(row.RetailPrice),
            DealerPrice = VndMoney.FormatDong(row.DealerPrice),
            DistributorPrice = VndMoney.FormatDong(row.DistributorPrice),
            ProjectPrice = VndMoney.FormatDong(row.ProjectPrice)
        };
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
        Form.CostPrice = string.IsNullOrWhiteSpace(Form.CostPrice) ? "0 đ" : Form.CostPrice;
        Form.RetailPrice = string.IsNullOrWhiteSpace(Form.RetailPrice) ? "0 đ" : Form.RetailPrice;
        Form.DealerPrice = string.IsNullOrWhiteSpace(Form.DealerPrice) ? "0 đ" : Form.DealerPrice;
        Form.DistributorPrice = string.IsNullOrWhiteSpace(Form.DistributorPrice) ? "0 đ" : Form.DistributorPrice;
        Form.ProjectPrice = string.IsNullOrWhiteSpace(Form.ProjectPrice) ? "0 đ" : Form.ProjectPrice;
    }

    private static string MeasureText(decimal? amount) =>
        amount is null ? string.Empty : VndMoney.FormatMeasure(amount.Value);

    private CatalogListFilter CurrentFilter() => CatalogListFilter.From(CategoryToken, Search);

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
            parts.Add("xem=" + detail.ToString("D"));
        }

        if (priceId is Guid price)
        {
            parts.Add("gia=" + price.ToString("D"));
        }

        if (add)
        {
            parts.Add("them=true");
        }

        parts.AddRange(FilterParts());
        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/danh-muc-gia" : "/danh-muc-gia?" + string.Join('&', parts);
    }

    private List<string> FilterParts()
    {
        var filter = CurrentFilter();
        var parts = new List<string>();
        if (filter.Category is ProductCategory category)
        {
            parts.Add("nganh=" + ProductCategoryToken.From(category));
        }
        else if (filter.CategoryRejected && !string.IsNullOrWhiteSpace(CategoryToken))
        {
            parts.Add("nganh=" + Uri.EscapeDataString(CategoryToken.Trim()));
        }

        if (filter.Term is not null)
        {
            parts.Add("q=" + Uri.EscapeDataString(filter.Term));
        }

        return parts;
    }
}
