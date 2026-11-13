using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Web.Pages.Audit;

[Authorize(Roles = EmployeeRole.Admin + "," + EmployeeRole.Director)]
public class IndexModel(ListAuditHandler list) : PageModel
{
    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];

    public int Total { get; private set; }

    public int PageNumber { get; private set; } = 1;

    public int PageCount => Math.Max(1, (Total + ListAuditHandler.PageSize - 1) / ListAuditHandler.PageSize);

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        var result = await list.HandleAsync(Search, PageNumber, cancellationToken);
        Entries = result.Entries;
        Total = result.Total;
    }

    public string PageUrl(int page) =>
        "/audit?" + (string.IsNullOrWhiteSpace(Search) ? string.Empty : "q=" + Uri.EscapeDataString(Search.Trim()) + "&") + "pageNumber=" + page;

    public static string Label(string action) => action switch
    {
        AuditActions.LoginSucceeded => "Đăng nhập thành công",
        AuditActions.LoginFailed => "Đăng nhập thất bại",
        AuditActions.LoginLockedOut => "Khóa tài khoản (sai quá 5 lần)",
        AuditActions.TwoFactorFailed => "Sai mã 2FA",
        AuditActions.TwoFactorEnabled => "Bật 2FA",
        AuditActions.TwoFactorDisabled => "Tắt 2FA",
        AuditActions.TwoFactorReset => "Admin tắt 2FA của nhân viên",
        AuditActions.RecoveryCodeUsed => "Dùng mã khôi phục 2FA",
        AuditActions.PasswordChanged => "Đổi mật khẩu",
        AuditActions.PasswordReset => "Admin đặt lại mật khẩu",
        AuditActions.EmployeeCreated => "Tạo nhân viên",
        AuditActions.EmployeeUpdated => "Sửa nhân viên",
        AuditActions.EmployeeActivated => "Kích hoạt hoặc vô hiệu hóa nhân viên",
        AuditActions.EmployeeUnlocked => "Mở khóa tài khoản",
        AuditActions.LocationCreated => "Thêm kho/showroom",
        AuditActions.LocationUpdated => "Sửa kho/showroom",
        AuditActions.LocationDeleted => "Xóa kho/showroom",
        AuditActions.TransferShipped => "Xuất chuyển kho",
        AuditActions.TransferReceived => "Nhận hàng chuyển kho",
        AuditActions.TransferCancelled => "Hủy phiếu chuyển kho",
        AuditActions.StocktakeApproved => "Duyệt kiểm kê",
        AuditActions.AdjustmentApproved => "Duyệt điều chỉnh tồn",
        AuditActions.DisposalApproved => "Duyệt phiếu hủy",
        AuditActions.PricesChanged => "Đổi giá sản phẩm",
        AuditActions.PurchaseReceived => "Nhận đơn mua vào kho",
        AuditActions.PurchaseUpdated => "Sửa đơn mua",
        AuditActions.PurchaseChargeAdded => "Thêm chi phí đơn mua",
        AuditActions.PurchaseChargeDeleted => "Xóa chi phí đơn mua",
        AuditActions.PurchaseAttachmentAdded => "Đính kèm chứng từ đơn mua",
        AuditActions.PurchaseAttachmentDownloaded => "Tải chứng từ đơn mua",
        AuditActions.PurchaseAttachmentDeleted => "Xóa chứng từ đơn mua",
        _ => action
    };
}
