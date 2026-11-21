using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Locations;
using Pancras.Domain.Inventory;
using Pancras.Web.Formatting;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Locations;

[Authorize(Roles = LocationAccess.ViewRoles)]
public class IndexModel(
    ListLocationsHandler list,
    CreateLocationCommandHandler create,
    UpdateLocationCommandHandler update,
    DisableLocationCommandHandler disable) : PageModel
{
    public IReadOnlyList<StockLocation> Locations { get; private set; } = [];

    public bool CanManage => LocationAccess.CanManage(User.ToActor().Role);

    public bool ShowForm { get; private set; }

    public LocationError Error { get; private set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public bool HasAny { get; private set; }


    [BindProperty]
    public CreateLocationCommand Command { get; set; } = new();

    public Guid? EditId { get; private set; }

    public string? ManageError { get; private set; }

    [BindProperty]
    public UpdateLocationCommand Edit { get; set; } = new();

    /// <summary>Danh sách đang lọc (ô tìm). Mọi link và chuyển trang sau thao tác giữ từ khóa (docs/03-ui-ux/components.md mục 10).</summary>
    public string ListUrl => ListState(new ListQuery("/locations")).ToString();

    public string FormUrl => ListState(new ListQuery("/locations").Add("form", "1")).ToString();

    public string EditUrl(Guid id) => ListState(new ListQuery("/locations").Add("edit", id)).ToString();

    /// <summary>Địa chỉ form thao tác (thêm, sửa, Disable) mang từ khóa đang tìm.</summary>
    public string ActionUrl(string? handler) => ListState(new ListQuery("/locations")).WithHandler(handler);

    private ListQuery ListState(ListQuery query) => query.Add("q", Search);

    public async Task OnGetAsync(string? form, string? edit, CancellationToken cancellationToken)
    {
        ShowForm = form == "1" && CanManage;
        if (CanManage && Guid.TryParse(edit, out var editId))
        {
            await LoadAsync(cancellationToken);
            var target = Locations.FirstOrDefault(location => location.Id == editId);
            if (target is not null)
            {
                EditId = editId;
                Edit = new UpdateLocationCommand { Id = editId, Version = target.Version, Name = target.Name, Kind = target.Kind };
            }
        }

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

        TempData.Toast("Đã thêm kho/showroom.");
        return Redirect(ListUrl);
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        var error = await update.HandleAsync(Edit, User.ToActor(), cancellationToken);
        if (error != LocationManageError.None)
        {
            ManageError = ManageErrorText(error);
            EditId = Edit.Id;
            await LoadAsync(cancellationToken);
            return Page();
        }

        TempData.Toast("Đã cập nhật kho/showroom.");
        return Redirect(ListUrl);
    }

    public async Task<IActionResult> OnPostDisableAsync(Guid id, CancellationToken cancellationToken)
    {
        var error = await disable.HandleAsync(id, User.ToActor(), cancellationToken);
        if (error == LocationManageError.None)
        {
            TempData.Toast("Đã Disable kho/showroom.");
        }
        else
        {
            TempData.Toast(ManageErrorText(error), ToastLevel.Error);
        }

        return Redirect(ListUrl);
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

    public static string ManageErrorText(LocationManageError error) => error switch
    {
        LocationManageError.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
        LocationManageError.NotFound => "Không tìm thấy kho/showroom.",
        LocationManageError.VersionConflict => "Kho/showroom vừa được người khác cập nhật. Đóng form, tải lại trang rồi sửa lại.",
        LocationManageError.NameMissing => "Cần nhập tên.",
        LocationManageError.NameTooLong => "Tên quá dài (tối đa 200 ký tự).",
        LocationManageError.InvalidKind => "Loại không hợp lệ.",
        LocationManageError.DuplicateName => "Đã có kho/showroom trùng tên.",
        LocationManageError.HasStock => "Không Disable được: kho/showroom còn tồn hàng. Hãy chuyển hết hàng đi trước.",
        LocationManageError.HasActiveEmployees => "Không Disable được: còn nhân viên đang hoạt động gắn với địa điểm này.",
        LocationManageError.HasOpenDocuments => "Không Disable được: còn đơn mua hoặc phiếu chuyển chưa xử lý liên quan đến kho này.",
        _ => "Không thực hiện được. Thử lại sau."
    };

    public static string KindLabel(StockLocationKind kind) =>
        kind == StockLocationKind.Showroom ? "Showroom" : "Kho";
}
