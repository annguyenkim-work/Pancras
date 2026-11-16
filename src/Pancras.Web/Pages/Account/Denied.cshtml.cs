using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pancras.Web.Pages.Account;

public class DeniedModel : PageModel
{
    public void OnGet()
    {
        // Trang từ chối quyền trả 403 thật (docs/03-ui-ux/role-based-ui.md), không phải 200.
        Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
