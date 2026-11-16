namespace Pancras.Domain.Auditing;

/// <summary>
/// Bản ghi có dấu vết và <c>Version</c> (concurrency token). Tầng lưu trữ tăng <c>Version</c> mỗi khi bản ghi
/// có thay đổi thật, nên hai người sửa cùng lúc thì lần lưu sau bị từ chối thay vì ghi đè.
/// </summary>
public abstract class VersionedAuditStamped : AuditStamped
{
    public int Version { get; private set; }
}
