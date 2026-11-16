using Pancras.Domain.Auditing;

namespace Pancras.Application.Auditing;

public interface IAuditLog
{
    /// <summary>Ghi ngay một dòng nhật ký. Lỗi ghi nhật ký không được làm hỏng thao tác chính.</summary>
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntry>> ListAsync(string? search, int pageNumber, int pageSize, bool newestFirst, CancellationToken cancellationToken);

    Task<int> CountAsync(string? search, CancellationToken cancellationToken);
}
