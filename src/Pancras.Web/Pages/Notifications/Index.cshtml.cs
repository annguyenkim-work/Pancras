using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Web.Pages.Notifications;

/// <summary>Chuông trên header dẫn về đây. Mỗi người chỉ thấy và mở được thông báo của mình.</summary>
[Authorize]
public class IndexModel(ListStockNoticesHandler handler) : PageModel
{
    public IReadOnlyList<StockNotice> Notices { get; private set; } = [];

    public int PageNumber { get; private set; } = 1;

    public int PageSize { get; private set; } = Pancras.Application.Common.PageSizes.Default;

    public int Total { get; private set; }

    public Pancras.Web.Formatting.PagerModel Pager => new(PageNumber, PageSize, Total, (page, size) => "/notifications?pageNumber=" + page + (size == Pancras.Application.Common.PageSizes.Default ? string.Empty : "&size=" + size));

    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        PageSize = Pancras.Web.Formatting.PagerModel.SizeFrom(Request);
        var all = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId)
            ? (await handler.HandleAsync(employeeId, cancellationToken)).OrderBy(notice => notice.Seen).ThenByDescending(notice => notice.CreatedAt).ToArray()
            : [];
        Total = all.Length;
        var pages = Math.Max(1, (Total + PageSize - 1) / PageSize);
        PageNumber = Math.Clamp(pageNumber, 1, pages);
        Notices = all.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToArray();
    }

    /// <summary>Đánh dấu đã xem rồi mở đúng chứng từ: phiếu chuyển mở đúng phiếu, kiểm kê/điều chỉnh/hủy mở tab kiểm kê.</summary>
    public async Task<IActionResult> OnPostOpenAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        StockNotice? notice = null;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId))
        {
            notice = await handler.OpenAsync(noticeId, employeeId, cancellationToken);
        }

        return Redirect(TargetOf(notice));
    }

    public static string TargetOf(StockNotice? notice) => notice switch
    {
        null => "/notifications",
        { Kind: StockNoticeKind.Purchase } => $"/purchasing?order={notice.DocumentId:D}",
        { Kind: StockNoticeKind.Transfer } => $"/inventory?view=transfers&open={notice.DocumentId:D}",
        _ => $"/inventory?view=stocktakes#document-{notice.DocumentId:D}"
    };
}
