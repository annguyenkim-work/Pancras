namespace Pancras.Domain.Auditing;

/// <summary>
/// Dấu vết bản ghi: ai ghi lần cuối và lúc nào. Lúc tạo cũng ghi vào hai cột này.
/// Giá trị do tầng lưu trữ ghi khi lưu (giờ UTC và mã nhân viên đang thao tác).
/// Thực thể kế thừa lớp này có hai cột updated_at, updated_by.
/// </summary>
public abstract class AuditStamped
{
    public const int ActorMaxLength = 64;

    /// <summary>Lần ghi cuối (UTC), kể cả lúc tạo.</summary>
    public DateTimeOffset UpdatedAt { get; protected set; }

    /// <summary>Mã nhân viên ghi lần cuối. Rỗng nghĩa là không rõ, "system" là tiến trình tự động.</summary>
    public string UpdatedBy { get; protected set; } = string.Empty;

    protected static string FitActor(string? by)
    {
        var trimmed = by?.Trim() ?? string.Empty;
        return trimmed.Length <= ActorMaxLength ? trimmed : trimmed[..ActorMaxLength];
    }
}
