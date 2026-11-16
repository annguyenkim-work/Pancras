using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;

namespace Pancras.Infrastructure.Persistence;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entry");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.OccurredAt).IsRequired();
        builder.Property(entry => entry.ActorCode).HasMaxLength(220).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(60).IsRequired();
        builder.Property(entry => entry.TargetType).HasMaxLength(60).IsRequired();
        builder.Property(entry => entry.TargetId).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Detail).HasMaxLength(AuditEntry.DetailMaxLength).IsRequired();
        builder.Property(entry => entry.IpAddress).HasMaxLength(64).IsRequired();
        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => entry.ActorEmployeeId);
    }
}

public sealed class AuditLog(PancrasDbContext context, ILogger<AuditLog> logger) : IAuditLog
{
    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await context.AuditEntries.AddAsync(entry, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not write audit entry {Action}.", entry.Action);
            context.Entry(entry).State = EntityState.Detached;
        }
    }


    public async Task<IReadOnlyList<AuditEntry>> ListPageAsync(
        AuditQuery filter,
        AuditCursor? cursor,
        bool backward,
        int limit,
        bool newestFirst,
        CancellationToken cancellationToken)
    {
        var rows = await ApplyCursor(Filter(filter), cursor, backward, newestFirst)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
        if (backward)
        {
            rows.Reverse();
        }

        return rows;
    }

    /// <summary>
    /// Keyset theo (giờ ghi, mã): cùng một giờ vẫn có thứ tự cố định nên không sót hay lặp dòng.
    /// Đi lùi thì đảo chiều sắp xếp để lấy các dòng sát dấu trang, rồi người gọi đảo lại.
    /// </summary>
    public static IQueryable<AuditEntry> ApplyCursor(IQueryable<AuditEntry> query, AuditCursor? cursor, bool backward, bool newestFirst)
    {
        var descending = newestFirst != backward;
        if (cursor is { } at)
        {
            var time = at.At;
            var id = at.Id;
            query = descending
                ? query.Where(entry => entry.OccurredAt < time || (entry.OccurredAt == time && entry.Id.CompareTo(id) < 0))
                : query.Where(entry => entry.OccurredAt > time || (entry.OccurredAt == time && entry.Id.CompareTo(id) > 0));
        }

        return descending
            ? query.OrderByDescending(entry => entry.OccurredAt).ThenByDescending(entry => entry.Id)
            : query.OrderBy(entry => entry.OccurredAt).ThenBy(entry => entry.Id);
    }

    private IQueryable<AuditEntry> Filter(AuditQuery filter)
    {
        var query = context.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            var actor = filter.Actor.Trim().ToLower();
            query = query.Where(entry => entry.ActorCode.ToLower().Contains(actor));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim();
            query = query.Where(entry => entry.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(filter.Target))
        {
            var target = filter.Target.Trim();
            query = query.Where(entry => entry.TargetType == target);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(entry => entry.OccurredAt >= from);
        }

        if (filter.ToUtcExclusive is { } to)
        {
            query = query.Where(entry => entry.OccurredAt < to);
        }

        if (string.IsNullOrWhiteSpace(filter.Search))
        {
            return query;
        }

        var term = filter.Search.Trim().ToLower();
        return query.Where(entry =>
            entry.Action.ToLower().Contains(term)
            || entry.ActorCode.ToLower().Contains(term)
            || entry.TargetId.ToLower().Contains(term)
            || entry.Detail.ToLower().Contains(term)
            || entry.IpAddress.Contains(term));
    }
}
