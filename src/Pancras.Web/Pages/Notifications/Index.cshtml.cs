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

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Notices = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId)
            ? (await handler.HandleAsync(employeeId, cancellationToken)).OrderBy(notice => notice.Seen).ThenByDescending(notice => notice.CreatedAt).ToArray()
            : [];
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
