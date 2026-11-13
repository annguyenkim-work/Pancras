using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Locations;

[Authorize(Roles = LocationAccess.ViewRoles)]
public class IndexModel(ListLocationsHandler list, CreateLocationCommandHandler create) : PageModel
{
    public IReadOnlyList<StockLocation> Locations { get; private set; } = [];

    public bool CanManage => LocationAccess.CanManage(User.ToActor().Role);

    public bool ShowForm { get; private set; }

    public LocationError Error { get; private set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public bool HasAny { get; private set; }

    public string? Notice { get; private set; }

    [BindProperty]
    public CreateLocationCommand Command { get; set; } = new();

    public async Task OnGetAsync(string? form, CancellationToken cancellationToken)
    {
        ShowForm = form == "1" && CanManage;
        Notice = TempData["LocationCreated"] as string;
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await create.HandleAsync(Command, User.ToActor(), cancellationToken);
        if (!result.IsSuccess)
        {
            Error = result.Error;
            ShowForm = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        TempData["LocationCreated"] = "Đã thêm kho/showroom.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var all = await list.HandleAsync(cancellationToken);
        HasAny = all.Count > 0;
        var term = Search?.Trim();
        Locations = string.IsNullOrEmpty(term)
            ? all
            : all.Where(location => location.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public static string ErrorText(LocationError error) => error switch
    {
        LocationError.Forbidden => "Bạn không có quyền thêm kho/showroom.",
        LocationError.NameMissing => "Cần nhập tên.",
        LocationError.NameTooLong => "Tên quá dài (tối đa 200 ký tự).",
        LocationError.InvalidKind => "Loại không hợp lệ.",
        LocationError.DuplicateName => "Đã có kho/showroom trùng tên.",
        _ => string.Empty
    };

    public static string KindLabel(StockLocationKind kind) =>
        kind == StockLocationKind.Showroom ? "Showroom" : "Kho";
}
