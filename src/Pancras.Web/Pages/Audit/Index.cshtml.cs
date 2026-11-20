using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;

namespace Pancras.Web.Pages.Audit;

[Authorize(Roles = AuditAccess.ViewRoles)]
public class IndexModel(ListAuditHandler list) : PageModel
{
    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];

    private string? _next;

    private string? _previous;

    public Pancras.Application.Common.ListSort Sort => Pancras.Web.Formatting.PagerModel.TokenFrom(Request, "sort") is { } token ? Pancras.Application.Common.ListSort.Parse(token, ["time"], "time") : new("time", true);

    public string SortUrl() => PageUrl(null, false, PageSize, Sort.Toggle("time").Token);

    public int PageSize { get; private set; } = Pancras.Application.Common.PageSizes.Default;

    /// <summary>Nhật ký rất lớn nên chuyển trang kiểu keyset: chỉ Trước/Sau, không đánh số trang và không đếm tổng.</summary>
    public Pancras.Web.Formatting.KeysetPagerModel Pager => new(
        Entries.Count,
        PageSize,
        _previous is null ? null : PageUrl(_previous, true, PageSize),
        _next is null ? null : PageUrl(_next, false, PageSize),
        size => PageUrl(null, false, size));

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

    public async Task OnGetAsync(string? after = null, string? before = null, CancellationToken cancellationToken = default)
    {
        PageSize = Pancras.Web.Formatting.PagerModel.SizeFrom(Request);
        var backward = string.IsNullOrEmpty(after) && !string.IsNullOrEmpty(before);
        var result = await list.HandleAsync(Query, backward ? before : after, backward, PageSize, Sort.Descending, cancellationToken);
        Entries = result.Entries;
        _next = result.Next;
        _previous = result.Previous;
    }

    private string PageUrl(string? cursor, bool backward, int size, string? sort = null)
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

        if (cursor is not null)
        {
            parts.Add((backward ? "before=" : "after=") + cursor);
        }

        return parts.Count == 0 ? "/audit" : "/audit?" + string.Join('&', parts);
    }

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }
}
