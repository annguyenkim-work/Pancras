using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Web.Pages.Audit;

[Authorize(Roles = AuditAccess.ViewRoles)]
public class IndexModel(ListAuditHandler list) : PageModel
{
    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];

    public int Total { get; private set; }

    public int PageNumber { get; private set; } = 1;

    public Pancras.Application.Common.ListSort Sort => Pancras.Web.Formatting.PagerModel.TokenFrom(Request, "sort") is { } token ? Pancras.Application.Common.ListSort.Parse(token, ["time"], "time") : new("time", true);

    public string SortUrl() => PageUrl(1, PageSize, Sort.Toggle("time").Token);

    public int PageSize { get; private set; } = Pancras.Application.Common.PageSizes.Default;

    public Pancras.Web.Formatting.PagerModel Pager => new(PageNumber, PageSize, Total, (page, size) => PageUrl(page, size));

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = Pancras.Web.Formatting.PagerModel.SizeFrom(Request);
        var result = await list.HandleAsync(Search, PageNumber, PageSize, Sort.Descending, cancellationToken);
        Entries = result.Entries;
        Total = result.Total;
    }

    public string PageUrl(int page, int size, string? sort = null) =>
        "/audit?" + (sort ?? Sort.Token) switch { "-time" => string.Empty, var order => "sort=" + order + "&" } + (string.IsNullOrWhiteSpace(Search) ? string.Empty : "q=" + Uri.EscapeDataString(Search.Trim()) + "&") + (size == Pancras.Application.Common.PageSizes.Default ? string.Empty : "size=" + size + "&") + "pageNumber=" + page;
}
