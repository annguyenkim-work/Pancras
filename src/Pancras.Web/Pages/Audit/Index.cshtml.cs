using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Web.Pages.Audit;

[Authorize(Roles = AuditAccess.ViewRoles)]
public class IndexModel(ListAuditHandler list) : PageModel
{
    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];

    public int Total { get; private set; }

    public int PageNumber { get; private set; } = 1;

    public int PageSize { get; private set; } = Pancras.Application.Common.PageSizes.Default;

    public Pancras.Web.Formatting.PagerModel Pager => new(PageNumber, PageSize, Total, PageUrl);

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = Pancras.Web.Formatting.PagerModel.SizeFrom(Request);
        var result = await list.HandleAsync(Search, PageNumber, PageSize, cancellationToken);
        Entries = result.Entries;
        Total = result.Total;
    }

    public string PageUrl(int page, int size) =>
        "/audit?" + (string.IsNullOrWhiteSpace(Search) ? string.Empty : "q=" + Uri.EscapeDataString(Search.Trim()) + "&") + (size == Pancras.Application.Common.PageSizes.Default ? string.Empty : "size=" + size + "&") + "pageNumber=" + page;

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
        AuditActions.EmployeeRoleChanged => "Đổi vai trò hoặc phân công",
        AuditActions.EmployeeActivated => "Kích hoạt hoặc vô hiệu hóa nhân viên",
        AuditActions.EmployeeDeactivated => "Ngừng hoạt động nhân viên",
        AuditActions.EmployeeUnlocked => "Mở khóa tài khoản",
        AuditActions.AccessDenied => "Bị từ chối do thiếu quyền",
        AuditActions.LocationCreated => "Thêm kho/showroom",
        AuditActions.LocationUpdated => "Sửa kho/showroom",
        AuditActions.LocationDeleted => "Disable kho/showroom",
        AuditActions.TransferRequested => "Đề nghị chuyển kho",
        AuditActions.TransferUpdated => "Sửa phiếu đề nghị chuyển kho",
        AuditActions.TransferApproved => "Duyệt phiếu chuyển kho",
        AuditActions.TransferRejected => "Từ chối phiếu chuyển kho",
        AuditActions.TransferDiscrepancyResolved => "Xử lý chênh lệch chuyển kho",
        AuditActions.TransferAttachmentAdded => "Đính kèm file phiếu chuyển kho",
        AuditActions.TransferAttachmentDownloaded => "Tải file phiếu chuyển kho",
        AuditActions.TransferShipped => "Xuất chuyển kho",
        AuditActions.TransferReceived => "Nhận hàng chuyển kho",
        AuditActions.TransferCancelled => "Hủy phiếu chuyển kho",
        AuditActions.StocktakeApproved => "Duyệt kiểm kê",
        AuditActions.AdjustmentApproved => "Duyệt điều chỉnh tồn",
        AuditActions.DisposalApproved => "Duyệt phiếu hủy",
        AuditActions.PricesChanged => "Đổi giá sản phẩm",
        AuditActions.CatalogItemDisabled => "Disable mặt hàng",
        AuditActions.CatalogItemActivated => "Active mặt hàng",
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
