using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Web.Navigation;

namespace Pancras.Web.Pages;

public class ComingSoonModel : PageModel
{
    public const string Message = "Chức năng đang được triển khai";

    public static IEnumerable<string> Routes => NavigationMenu.Items.Where(item => item.ComingSoon).Select(item => item.Href);

    public string Title { get; private set; } = Message;

    public void OnGet()
    {
        var path = HttpContext.Request.Path.Value ?? string.Empty;
        Title = NavigationMenu.TitleOf(path) ?? Message;
    }
}
