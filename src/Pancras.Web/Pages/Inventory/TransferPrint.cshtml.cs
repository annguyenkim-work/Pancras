using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;
using Pancras.Web.Formatting;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Inventory;

/// <summary>Bản in phiếu xuất kho (chuyển kho nội bộ). Quyền và trạng thái được kiểm ở handler; bản in không có giá vốn.</summary>
[Authorize(Roles = InventoryAccess.ViewRoles)]
public class TransferPrintModel(GetTransferPrintHandler handler, TimeProvider clock) : PageModel
{
    public TransferSlipRow Slip { get; private set; } = null!;

    public string Code => StockLabels.TransferCode(Slip.Number);

    public string PrintedAt => VietnamTime.Format(clock.GetUtcNow());

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTransferPrintQuery(id, User.ToActor()), cancellationToken);
        if (result.IsSuccess)
        {
            Slip = result.Value!;
            return Page();
        }

        return result.Error == StockError.Forbidden ? Forbid() : NotFound();
    }
}
