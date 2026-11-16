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


    public async Task<IReadOnlyList<AuditEntry>> ListAsync(AuditQuery filter, int pageNumber, int pageSize, bool newestFirst, CancellationToken cancellationToken) =>
        await (newestFirst ? Filter(filter).OrderByDescending(entry => entry.OccurredAt) : Filter(filter).OrderBy(entry => entry.OccurredAt))
            .Skip((Math.Max(1, pageNumber) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(AuditQuery filter, CancellationToken cancellationToken) =>
        Filter(filter).CountAsync(cancellationToken);

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
