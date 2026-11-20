using Pancras.Domain.Auditing;

namespace Pancras.Application.Auditing;

public interface IAuditLog
{
    /// <summary>
    /// Thêm một dòng nhật ký vào ngữ cảnh hiện tại, **chưa lưu**: nó được lưu cùng giao dịch khi <c>IUnitOfWorkScope.CommitAsync</c>
    /// (dữ liệu và nhật ký cùng lưu hoặc cùng hủy). Dùng cho thao tác có ghi dữ liệu.
    /// </summary>
    void Stage(AuditEntry entry);

    /// <summary>Ghi ngay một dòng nhật ký, riêng một lần lưu, cho sự kiện không kèm ghi dữ liệu (từ chối quyền, xuất file, tải chứng từ). Lỗi chỉ log, không làm hỏng thao tác chính.</summary>
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Tối đa <paramref name="limit"/> dòng theo thứ tự hiển thị, ngay sau (hoặc, khi <paramref name="backward"/>, ngay trước) dấu trang; không có dấu trang thì từ đầu.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> ListPageAsync(AuditQuery query, AuditCursor? cursor, bool backward, int limit, bool newestFirst, CancellationToken cancellationToken);
}
