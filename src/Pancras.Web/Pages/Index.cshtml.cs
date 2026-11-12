using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pancras.Web.Pages;

public class IndexModel : PageModel
{
    public string EmployeeName => User.Identity?.Name ?? string.Empty;

    public string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public void OnGet()
    {
    }
}
