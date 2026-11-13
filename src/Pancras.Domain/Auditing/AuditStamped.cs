namespace Pancras.Domain.Auditing;

/// <summary>
/// Dấu vết bản ghi: ai tạo, ai sửa lần cuối và lúc nào. Giá trị do tầng lưu trữ ghi tự động khi lưu
/// (thời điểm UTC và mã nhân viên của người đang thao tác), nên handler không phải nhớ gọi.
/// Thực thể kế thừa lớp này sẽ có bốn cột created_at, created_by, updated_at, updated_by.
/// </summary>
public abstract class AuditStamped
{
    public const int ActorMaxLength = 64;

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>Mã nhân viên tạo. Rỗng nghĩa là không rõ (dữ liệu cũ), "system" là tiến trình tự động.</summary>
    public string CreatedBy { get; protected set; } = string.Empty;

    /// <summary>Lần sửa cuối (UTC); null nếu chưa từng sửa.</summary>
    public DateTimeOffset? UpdatedAt { get; protected set; }

    public string UpdatedBy { get; protected set; } = string.Empty;

    protected static string FitActor(string? by)
    {
        var trimmed = by?.Trim() ?? string.Empty;
        return trimmed.Length <= ActorMaxLength ? trimmed : trimmed[..ActorMaxLength];
    }
}
