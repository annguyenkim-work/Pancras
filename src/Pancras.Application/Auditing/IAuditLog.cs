using Pancras.Domain.Auditing;

namespace Pancras.Application.Auditing;

public interface IAuditLog
{
    /// <summary>Ghi ngay một dòng nhật ký. Lỗi ghi nhật ký không được làm hỏng thao tác chính.</summary>
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Tối đa <paramref name="limit"/> dòng theo thứ tự hiển thị, ngay sau (hoặc, khi <paramref name="backward"/>, ngay trước) dấu trang; không có dấu trang thì từ đầu.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> ListPageAsync(AuditQuery query, AuditCursor? cursor, bool backward, int limit, bool newestFirst, CancellationToken cancellationToken);
}
