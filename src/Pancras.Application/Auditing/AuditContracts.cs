using Pancras.Domain.Auditing;

namespace Pancras.Application.Auditing;

public static class AuditLogExtensions
{
    public static Task RecordAsync(
        this IAuditLog? log,
        TimeProvider clock,
        IRequestInfo? request,
        Guid? actorId,
        string? actorCode,
        string action,
        string? targetType,
        string? targetId,
        string? detail,
        CancellationToken cancellationToken) =>
        log is null
            ? Task.CompletedTask
            : log.RecordAsync(
                AuditEntry.Create(clock.GetUtcNow(), actorId ?? request?.ActorId, actorCode ?? request?.ActorCode, action, targetType, targetId, detail, request?.IpAddress),
                cancellationToken);
}

public sealed class ListAuditHandler(IAuditLog log)
{
    /// <summary>Một trang nhật ký theo keyset: đọc dư một dòng để biết còn trang tiếp hay không.</summary>
    public async Task<AuditPage> HandleAsync(
        AuditQuery query,
        string? cursorToken,
        bool backward,
        int pageSize,
        bool newestFirst,
        CancellationToken cancellationToken)
    {
        var size = pageSize < 1 ? Pancras.Application.Common.PageSizes.Default : pageSize;
        var cursor = AuditCursor.TryParse(cursorToken);
        backward &= cursor is not null;
        var rows = await log.ListPageAsync(query, cursor, backward, size + 1, newestFirst, cancellationToken);
        var hasMore = rows.Count > size;
        var entries = hasMore
            ? (backward ? rows.Skip(rows.Count - size).ToArray() : rows.Take(size).ToArray())
            : rows.ToArray();
        if (entries.Length == 0)
        {
            return new AuditPage(entries, null, null);
        }

        // Có dấu trang nghĩa là ta đến từ một phía nên phía đó chắc chắn còn dòng; phía kia còn hay không tùy hasMore.
        var next = backward ? AuditCursor.Of(entries[^1]).Encode() : hasMore ? AuditCursor.Of(entries[^1]).Encode() : null;
        var previous = backward ? hasMore ? AuditCursor.Of(entries[0]).Encode() : null : cursor is not null ? AuditCursor.Of(entries[0]).Encode() : null;
        return new AuditPage(entries, next, previous);
    }
}

/// <summary>Dấu vết bản ghi để hiển thị: ai ghi lần cuối và lúc nào (mã nhân viên, giờ UTC). Lúc tạo cũng là một lần ghi.</summary>
public sealed record AuditStampView(DateTimeOffset UpdatedAt, string UpdatedBy)
{
    public static AuditStampView From(Pancras.Domain.Auditing.AuditStamped entity) =>
        new(entity.UpdatedAt, entity.UpdatedBy);
}
