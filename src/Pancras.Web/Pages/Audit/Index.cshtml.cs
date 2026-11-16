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

    [BindProperty(SupportsGet = true, Name = "actor")]
    public string? Actor { get; set; }

    [BindProperty(SupportsGet = true, Name = "action")]
    public string? ActionCode { get; set; }

    [BindProperty(SupportsGet = true, Name = "target")]
    public string? Target { get; set; }

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    private AuditQuery Query => new(Search, Actor, ActionCode, Target, From, To);

    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = Pancras.Web.Formatting.PagerModel.SizeFrom(Request);
        var result = await list.HandleAsync(Query, PageNumber, PageSize, Sort.Descending, cancellationToken);
        Entries = result.Entries;
        Total = result.Total;
    }

    public string PageUrl(int page, int size, string? sort = null)
    {
        var parts = new List<string>();
        var order = sort ?? Sort.Token;
        if (order != "-time")
        {
            parts.Add("sort=" + order);
        }

        Add(parts, "q", Search);
        Add(parts, "actor", Actor);
        Add(parts, "action", ActionCode);
        Add(parts, "target", Target);
        Add(parts, "from", From?.ToString("yyyy-MM-dd"));
        Add(parts, "to", To?.ToString("yyyy-MM-dd"));
        if (size != Pancras.Application.Common.PageSizes.Default)
        {
            parts.Add("size=" + size);
        }

        parts.Add("pageNumber=" + page);
        return "/audit?" + string.Join('&', parts);
    }

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }
}
