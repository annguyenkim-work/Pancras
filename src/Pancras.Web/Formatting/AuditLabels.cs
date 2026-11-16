using Pancras.Domain.Auditing;

namespace Pancras.Web.Formatting;

/// <summary>Nơi duy nhất đổi mã hành động nhật ký (<c>nhóm.sự_kiện</c>) sang câu tiếng Việt; mã lạ giữ nguyên.</summary>
public static class AuditLabels
{
    /// <summary>Mọi mã hành động kèm nhãn tiếng Việt, sắp theo nhãn, cho ô chọn lọc.</summary>
    public static IReadOnlyList<(string Code, string Label)> Options { get; } = typeof(AuditActions).GetFields()
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .Select(code => (Code: code, Label: Of(code)))
        .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public static string Of(string action) => action switch
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
        AuditActions.StockDocumentAttachmentAdded => "Đính kèm file chứng từ kho",
        AuditActions.StockDocumentAttachmentDownloaded => "Tải file chứng từ kho",
        AuditActions.TransferReturned => "Trả hàng chuyển kho về kho xuất",
        AuditActions.PurchaseCreated => "Lập đơn mua",
        AuditActions.PurchaseExported => "Xuất đơn mua",
        AuditActions.PurchaseStatusChanged => "Đổi trạng thái đơn mua",
        AuditActions.PurchaseCostAdjusted => "Điều chỉnh chi phí đơn mua",
        AuditActions.SupplierCreated => "Thêm nhà cung cấp",
        AuditActions.SupplierUpdated => "Sửa nhà cung cấp",
        AuditActions.SupplierDeactivated => "Disable nhà cung cấp",
        AuditActions.SupplierReactivated => "Active nhà cung cấp",
        _ => action
    };
}
