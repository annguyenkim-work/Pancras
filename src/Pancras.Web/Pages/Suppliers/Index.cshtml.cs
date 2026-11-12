using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Suppliers;

namespace Pancras.Web.Pages.Suppliers;

[Authorize]
public class IndexModel(
    ListSupplierCardsHandler listHandler,
    CreateSupplierCommandHandler createHandler,
    GetSupplierProfileHandler profileHandler,
    UpdateSupplierCommandHandler updateHandler,
    DeleteSupplierCommandHandler deleteHandler) : PageModel
{
    public SupplierCardPage CardPage { get; private set; } = new([], 1, SupplierPageRequest.DefaultSize, 0);

    public IReadOnlyList<SupplierCard> Cards => CardPage.Cards;

    public SupplierError Error { get; private set; }

    public string ErrorTitle { get; private set; } = "Không lưu được nhà cung cấp";

    public IReadOnlyList<string> ErrorMessages { get; private set; } = [];

    public IReadOnlyList<SupplierIssue> Issues { get; private set; } = [];

    public bool ShowForm { get; private set; }

    public bool ShowEditForm { get; private set; }

    public bool ShowDelete { get; private set; }

    public string? DeleteName { get; private set; }

    public string? Notice { get; private set; }

    [BindProperty]
    public CreateSupplierCommand Command { get; set; } = new();

    [BindProperty]
    public UpdateSupplierCommand EditCommand { get; set; } = new();

    [BindProperty]
    public Guid DeleteId { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public int? PageNumber { get; private set; }

    public bool HasSearch => SupplierListFilter.From(Search).HasCriteria;

    public string FilteredListUrl => BuildUrl(CardPage.PageNumber);

    public string PageUrl(int page) => BuildUrl(page);

    public string EditUrl(Guid id) => BuildUrl(CardPage.PageNumber, id);

    public string AddUrl => BuildUrl(CardPage.PageNumber, add: true);

    public string DeleteUrl(Guid id) => BuildUrl(CardPage.PageNumber, deleteId: id);

    public bool HasIssue(SupplierField field) => Issues.Any(issue => issue.Field == field);

    public string FieldClass(SupplierField field)
    {
        var border = HasIssue(field) ? "border-rose-400" : "border-slate-200";
        return $"mt-1.5 w-full px-3 py-2 text-sm rounded-lg border {border} bg-white outline-none focus:border-slate-900";
    }

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

    public async Task<IActionResult> OnGetAsync(Guid? edit, Guid? delete, bool add, CancellationToken cancellationToken)
    {
        ReadPage();
        var requested = SupplierPageRequest.From(PageNumber);
        CardPage = await LoadCardsAsync(requested, cancellationToken);
        if (requested.Number != CardPage.PageNumber)
        {
            return Redirect(BuildUrl(CardPage.PageNumber, edit, delete, add));
        }

        Notice = TempData["SupplierNotice"] as string;

        if (delete is Guid deleteId)
        {
            var existing = await profileHandler.HandleAsync(deleteId, cancellationToken);
            if (existing.IsFailure)
            {
                RememberFailure("Không xóa được nhà cung cấp", existing.SupplierError, existing.SupplierIssues, null);
                return Page();
            }

            DeleteId = existing.Value!.Id;
            DeleteName = existing.Value.Name;
            ShowDelete = true;
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

        var profile = await profileHandler.HandleAsync(id, cancellationToken);
        if (profile.IsFailure)
        {
            RememberFailure("Không mở được nhà cung cấp", profile.SupplierError, profile.SupplierIssues, null);
            return Page();
        }

        EditCommand = new UpdateSupplierCommand
        {
            Id = profile.Value!.Id,
            Code = profile.Value.Code,
            Name = profile.Value.Name,
            Country = profile.Value.Country,
            ContactName = profile.Value.ContactName,
            Phone = profile.Value.Phone,
            Email = profile.Value.Email,
            Account = profile.Value.Account
        };
        ShowEditForm = true;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        var result = await createHandler.HandleAsync(Command, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không thêm được nhà cung cấp", result.SupplierError, result.SupplierIssues, Command.Code);
            ShowForm = true;
            CardPage = await LoadCardsAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var created = result.Value!;
        var page = await listHandler.PageContainingAsync(created.Name, created.Code, cancellationToken);
        TempData["SupplierNotice"] = "Đã thêm nhà cung cấp.";
        Search = null;
        return Redirect(BuildUrl(page));
    }

    public async Task<IActionResult> OnPostEditAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        var result = await updateHandler.HandleAsync(EditCommand, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không sửa được nhà cung cấp", result.SupplierError, result.SupplierIssues, EditCommand.Code);
            ShowEditForm = true;
            CardPage = await LoadCardsAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        var cards = await LoadCardsAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
        var visible = cards.Cards.Any(card => card.Id == result.Value!.Id);
        TempData["SupplierNotice"] = visible
            ? "Đã lưu nhà cung cấp."
            : "Đã lưu nhà cung cấp. Nhà cung cấp không còn trong kết quả đang xem.";
        return Redirect(BuildUrl(cards.PageNumber));
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        ReadPage();
        var result = await deleteHandler.HandleAsync(DeleteId, cancellationToken);
        if (result.IsFailure)
        {
            RememberFailure("Không xóa được nhà cung cấp", result.SupplierError, result.SupplierIssues, null);
            CardPage = await LoadCardsAsync(SupplierPageRequest.From(PageNumber), cancellationToken);
            return Page();
        }

        return Redirect(BuildUrl(SupplierPageRequest.From(PageNumber).Number));
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

    private Task<SupplierCardPage> LoadCardsAsync(SupplierPageRequest page, CancellationToken cancellationToken) =>
        listHandler.HandleAsync(SupplierListFilter.From(Search), page, cancellationToken);

    private string BuildUrl(int page, Guid? edit = null, Guid? deleteId = null, bool add = false)
    {
        var filter = SupplierListFilter.From(Search);
        var parts = new List<string>();
        if (edit is Guid id)
        {
            parts.Add("edit=" + id.ToString("D"));
        }

        if (deleteId is Guid delete)
        {
            parts.Add("delete=" + delete.ToString("D"));
        }

        if (add)
        {
            parts.Add("add=true");
        }

        if (filter.Term is not null)
        {
            parts.Add("q=" + Uri.EscapeDataString(filter.Term));
        }

        if (page > 1)
        {
            parts.Add("page=" + page.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/nha-cung-cap" : "/nha-cung-cap?" + string.Join('&', parts);
    }
}
