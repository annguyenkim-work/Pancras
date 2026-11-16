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
    public async Task<(IReadOnlyList<AuditEntry> Entries, int Total)> HandleAsync(
        string? search,
        int pageNumber,
        int pageSize,
        bool newestFirst,
        CancellationToken cancellationToken)
    {
        var page = pageNumber < 1 ? 1 : pageNumber;
        var total = await log.CountAsync(search, cancellationToken);
        var entries = await log.ListAsync(search, page, pageSize, newestFirst, cancellationToken);
        return (entries, total);
    }
}

/// <summary>Dấu vết bản ghi để hiển thị: ai ghi lần cuối và lúc nào (mã nhân viên, giờ UTC). Lúc tạo cũng là một lần ghi.</summary>
public sealed record AuditStampView(DateTimeOffset UpdatedAt, string UpdatedBy)
{
    public static AuditStampView From(Pancras.Domain.Auditing.AuditStamped entity) =>
        new(entity.UpdatedAt, entity.UpdatedBy);
}
