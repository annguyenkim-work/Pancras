using Pancras.Web.Formatting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Suppliers;

[Authorize(Roles = SupplierAccess.ViewRoles)]
public class IndexModel(
    ListSupplierCardsHandler listHandler,
    CreateSupplierCommandHandler createHandler,
    GetSupplierProfileHandler profileHandler,
    UpdateSupplierCommandHandler updateHandler,
    DeactivateSupplierCommandHandler deactivateHandler,
    ReactivateSupplierCommandHandler reactivateHandler) : PageModel
{
    public SupplierCardPage CardPage { get; private set; } = new([], 1, SupplierPageRequest.DefaultSize, 0);

    public IReadOnlyList<SupplierCard> Cards => CardPage.Cards;

    public IReadOnlyList<string> Countries { get; private set; } = [];

    public SupplierError Error { get; private set; }

    public string ErrorTitle { get; private set; } = "Không lưu được nhà cung cấp";

    public IReadOnlyList<string> ErrorMessages { get; private set; } = [];

    public IReadOnlyList<SupplierIssue> Issues { get; private set; } = [];

    /// <summary>Mở hộp thoại thêm hoặc sửa. Sửa khi <see cref="Form"/> có Id.</summary>
    public bool ShowForm { get; private set; }

    public bool IsEditing => Form.Id != Guid.Empty;

    /// <summary>Mở hộp thoại xác nhận ngừng hợp tác.</summary>
    public bool ShowStop { get; private set; }

    public SupplierProfile? StopProfile { get; private set; }


    /// <summary>Dùng chung cho thêm và sửa: Id rỗng là thêm mới.</summary>
    [BindProperty]
    public UpdateSupplierCommand Form { get; set; } = new();

    [BindProperty]
    public Guid StopId { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    /// <summary>"inactive" (đã ngừng hợp tác), "all" (tất cả); bỏ trống là đang hợp tác.</summary>
    [BindProperty(SupportsGet = true, Name = "status")]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true, Name = "country")]
    public string? Country { get; set; }

    public int? PageNumber { get; private set; }

    private EmployeeActor Actor => User.ToActor();

    public bool CanManage => SupplierAccess.CanManage(Actor.Role);

    public bool CanReactivate => SupplierAccess.CanReactivate(Actor.Role);

    public bool CanViewBank => SupplierAccess.CanViewBank(Actor.Role);

    public SupplierStatusFilter StatusFilter => Status?.Trim().ToLowerInvariant() switch
    {
        "inactive" => SupplierStatusFilter.Inactive,
        "all" => SupplierStatusFilter.All,
        _ => SupplierStatusFilter.Active
    };

    public bool HasSearch => Filter.HasCriteria;

    private SupplierListFilter Filter => SupplierListFilter.From(Search, StatusFilter, Country);

    public string FilteredListUrl => BuildUrl(CardPage.PageNumber);

    public string PageUrl(int page) => BuildUrl(page);

    public string EditUrl(Guid id) => BuildUrl(CardPage.PageNumber, id);

    public string AddUrl => BuildUrl(CardPage.PageNumber, add: true);

    public string StopUrl(Guid id) => BuildUrl(CardPage.PageNumber, stopId: id);

    /// <summary>Danh sách đơn mua của nhà cung cấp này (đơn chưa nhập kho nằm trong đó).</summary>
    public static string PurchasesUrl(Guid supplierId) => "/purchasing?supplier=" + supplierId.ToString("D");

    public bool HasIssue(SupplierField field) => Issues.Any(issue => issue.Field == field);

    public string FieldClass(SupplierField field)
    {
        var border = HasIssue(field) ? "border-rose-400" : "border-slate-200";
        return $"mt-1.5 w-full px-3 py-2 text-sm rounded-lg border {border} bg-white outline-none focus:border-slate-900";
    }

    public IReadOnlyList<string> CountryChoices => SupplierCountries.For(Form.Country);

    public IReadOnlyList<int> VisiblePages
    {
        get
        {
            const int window = 5;
            var total = CardPage.TotalPages;
            var current = CardPage.PageNumber;
            var start = Math.Max(1, current - window / 2);
            var end = Math.Min(total, start + window - 1);
            start = Math.Max(1, end - window + 1);
            return Enumerable.Range(start, end - start + 1).ToArray();
        }
    }

    public async Task<IActionResult> OnGetAsync(Guid? edit, Guid? stop, bool add, CancellationToken cancellationToken)
    {
        ReadPage();
        var requested = SupplierPageRequest.From(PageNumber);
        await LoadAsync(requested, cancellationToken);
        if (requested.Number != CardPage.PageNumber)
        {
            return Redirect(BuildUrl(CardPage.PageNumber, edit, stop, add));
        }

        if (!CanManage)
        {
            return Page();
        }

        if (stop is Guid stopId)
        {
            var existing = await profileHandler.HandleAsync(stopId, Actor, cancellationToken);
            if (existing.IsFailure)
            {
                RememberFailure("Không mở được nhà cung cấp", existing.SupplierError, existing.SupplierIssues, null);
                return Page();
            }

            StopId = existing.Value!.Id;
            StopProfile = existing.Value;
            ShowStop = true;
            return Page();
        }

        if (add)
        {
            ShowForm = true;
            return Page();
        }

        if (edit is not Guid id)
        {
            return Page();
        }

        var profile = await profileHandler.HandleAsync(id, Actor, cancellationToken);
        if (profile.IsFailure)
        {
            RememberFailure("Không mở được nhà cung cấp", profile.SupplierError, profile.SupplierIssues, null);
            return Page();
        }

        Form = new UpdateSupplierCommand
        {
            Id = profile.Value!.Id,
            Code = profile.Value.Code,
            Name = profile.Value.Name,
            Country = profile.Value.Country,
            ContactName = profile.Value.ContactName,
            Phone = profile.Value.Phone,
            Email = profile.Value.Email,
            BankName = profile.Value.BankName,
            Account = profile.Value.Account,
            SwiftCode = profile.Value.SwiftCode
        };
        ShowForm = true;
        return Page();
    }

    /// <summary>Lưu từ hộp thoại: thêm mới khi chưa có Id, ngược lại là sửa.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        Form.Actor = Actor;
        return IsEditing
            ? await SaveEditAsync(cancellationToken)
            : await SaveNewAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        var result = await deactivateHandler.HandleAsync(StopId, Actor, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không ngừng hợp tác được", result.SupplierError, result.SupplierIssues, null);
            await LoadAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        TempData.Toast("Đã chuyển nhà cung cấp sang trạng thái ngừng hợp tác. Xem lại ở bộ lọc \"Đã ngừng hợp tác\"; Admin có thể khôi phục.");
        return Redirect(BuildUrl(SupplierPageRequest.From(PageNumber).Number));
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        ReadPage();
        var result = await reactivateHandler.HandleAsync(id, Actor, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không khôi phục được nhà cung cấp", result.SupplierError, result.SupplierIssues, null);
            await LoadAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        TempData.Toast("Đã khôi phục nhà cung cấp. Nhà cung cấp lại xuất hiện khi lập đơn mua.");
        return Redirect(BuildUrl(SupplierPageRequest.From(PageNumber).Number));
    }

    private async Task<IActionResult> SaveNewAsync(CancellationToken cancellationToken)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        var result = await createHandler.HandleAsync(
            new CreateSupplierCommand
            {
                Code = Form.Code,
                Name = Form.Name,
                Country = Form.Country,
                ContactName = Form.ContactName,
                Phone = Form.Phone,
                Email = Form.Email,
                BankName = Form.BankName,
                Account = Form.Account,
                SwiftCode = Form.SwiftCode,
                Actor = Actor
            },
            cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không thêm được nhà cung cấp", result.SupplierError, result.SupplierIssues, Form.Code);
            ShowForm = true;
            await LoadAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var created = result.Value!;
        var page = await listHandler.PageContainingAsync(created.Name, created.Code, cancellationToken);
        TempData.Toast($"Đã thêm nhà cung cấp {created.Name}.");
        Search = null;
        Status = null;
        Country = null;
        return Redirect(BuildUrl(page));
    }

    private async Task<IActionResult> SaveEditAsync(CancellationToken cancellationToken)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        var result = await updateHandler.HandleAsync(Form, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không sửa được nhà cung cấp", result.SupplierError, result.SupplierIssues, Form.Code);
            ShowForm = true;
            await LoadAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var cards = await listHandler.HandleAsync(Filter, SupplierPageRequest.From(PageNumber), Actor, cancellationToken);
        var visible = cards.Cards.Any(card => card.Id == result.Value!.Id);
        TempData.Toast(visible
            ? $"Đã lưu nhà cung cấp {result.Value!.Name}."
            : $"Đã lưu nhà cung cấp {result.Value!.Name}. Nhà cung cấp không khớp bộ lọc hiện tại, hãy bỏ lọc để xem.");
        return Redirect(BuildUrl(cards.PageNumber));
    }

    private void ReadPage()
    {
        PageNumber = null;
        if (Request.HasFormContentType
            && Request.Form.TryGetValue("page", out var posted)
            && int.TryParse(posted, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var fromForm))
        {
            PageNumber = fromForm;
            return;
        }

        if (Request.Query.TryGetValue("page", out var query)
            && int.TryParse(query, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var fromQuery))
        {
            PageNumber = fromQuery;
        }
    }

    private void RememberFailure(
        string title,
        SupplierError error,
        IReadOnlyList<SupplierIssue> issues,
        string? code)
    {
        Error = error;
        ErrorTitle = title;
        Issues = issues;
        ErrorMessages = SupplierErrorMessages.Describe(error, issues, code);
    }

    private async Task LoadAsync(SupplierPageRequest page, CancellationToken cancellationToken)
    {
        CardPage = await listHandler.HandleAsync(Filter, page, Actor, cancellationToken);
        Countries = await listHandler.ListCountriesAsync(Actor, cancellationToken);
    }

    private string BuildUrl(int page, Guid? edit = null, Guid? stopId = null, bool add = false)
    {
        var filter = Filter;
        var parts = new List<string>();
        if (edit is Guid id)
        {
            parts.Add("edit=" + id.ToString("D"));
        }

        if (stopId is Guid stop)
        {
            parts.Add("stop=" + stop.ToString("D"));
        }

        if (add)
        {
            parts.Add("add=true");
        }

        if (filter.Term is not null)
        {
            parts.Add("q=" + Uri.EscapeDataString(filter.Term));
        }

        if (filter.Status == SupplierStatusFilter.Inactive)
        {
            parts.Add("status=inactive");
        }
        else if (filter.Status == SupplierStatusFilter.All)
        {
            parts.Add("status=all");
        }

        if (filter.Country is not null)
        {
            parts.Add("country=" + Uri.EscapeDataString(filter.Country));
        }

        if (page > 1)
        {
            parts.Add("page=" + page.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/suppliers" : "/suppliers?" + string.Join('&', parts);
    }
}
