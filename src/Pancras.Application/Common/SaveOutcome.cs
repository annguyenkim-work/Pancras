namespace Pancras.Application.Common;

/// <summary>
/// Kết quả lưu: lưu được, trùng khóa duy nhất (ví dụ mã), có người khác lưu chồng lên cùng bản ghi,
/// hoặc lỗi lưu khác (đã ghi log; giao dịch bị hủy).
/// </summary>
public enum SaveOutcome
{
    Saved = 0,
    Duplicate,
    Conflict,
    Failed
}

public static class SaveOutcomeExtensions
{
    /// <summary>
    /// Cho thao tác không có mã lỗi nghiệp vụ riêng cho "lưu không được" (đổi mật khẩu, 2FA, đăng nhập...): lưu được thì đi tiếp,
    /// người khác vừa sửa thì ném <see cref="SaveConflictException"/> (trang hiện thông báo "dữ liệu vừa được người khác cập nhật"),
    /// lỗi khác ném <see cref="InvalidOperationException"/>.
    /// </summary>
    public static void EnsureSaved(this SaveOutcome outcome)
    {
        switch (outcome)
        {
            case SaveOutcome.Saved:
                return;
            case SaveOutcome.Conflict:
                throw new SaveConflictException();
            default:
                throw new InvalidOperationException("Không lưu được dữ liệu.");
        }
    }

    /// <summary>
    /// Lỗi lưu không phải nghiệp vụ (<see cref="SaveOutcome.Failed"/>) là sự cố hệ thống: ném để <c>UseExceptionHandler</c> xử lý,
    /// giống khi <c>DbUpdateException</c> lọt ra trước đây. Các kết quả khác trả lại để handler map sang mã lỗi nghiệp vụ.
    /// </summary>
    public static SaveOutcome ThrowIfFailed(this SaveOutcome outcome) =>
        outcome == SaveOutcome.Failed ? throw new InvalidOperationException("Không lưu được dữ liệu.") : outcome;
}

/// <summary>Người khác đã lưu chồng lên cùng bản ghi (<see cref="SaveOutcome.Conflict"/>) ở thao tác không có mã lỗi nghiệp vụ riêng.</summary>
public sealed class SaveConflictException() : Exception("Dữ liệu vừa được người khác cập nhật.");
