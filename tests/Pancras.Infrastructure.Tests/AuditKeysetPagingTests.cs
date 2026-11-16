using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

public class AuditKeysetPagingTests
{
    private readonly DbContextOptions<PancrasDbContext> _options = new DbContextOptionsBuilder<PancrasDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Bảy dòng; ba dòng cuối cùng giờ để thử tie-break theo mã.</summary>
    private async Task<List<AuditEntry>> SeedAsync()
    {
        await using var context = new PancrasDbContext(_options);
        var entries = new List<AuditEntry>();
        for (var i = 0; i < 4; i++)
        {
            entries.Add(AuditEntry.Create(Start.AddMinutes(i), null, "an" + i, AuditActions.LoginSucceeded));
        }

        for (var i = 0; i < 3; i++)
        {
            entries.Add(AuditEntry.Create(Start.AddMinutes(10), null, "tie" + i, AuditActions.LoginSucceeded));
        }

        context.AuditEntries.AddRange(entries);
        await context.SaveChangesAsync();
        return entries;
    }

    private static ListAuditHandler Handler(PancrasDbContext context) =>
        new(new AuditLog(context, NullLogger<AuditLog>.Instance));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WalkingForward_VisitsEveryEntryExactlyOnce_EvenWithEqualTimestamps(bool newestFirst)
    {
        var seeded = await SeedAsync();
        await using var context = new PancrasDbContext(_options);
        var handler = Handler(context);
        var seen = new List<Guid>();
        string? cursor = null;

        for (var guard = 0; guard < 10; guard++)
        {
            var page = await handler.HandleAsync(new AuditQuery(), cursor, false, 3, newestFirst, default);
            seen.AddRange(page.Entries.Select(entry => entry.Id));
            if (page.Next is null)
            {
                break;
            }

            cursor = page.Next;
        }

        Assert.Equal(seeded.Count, seen.Count);
        Assert.Equal(seeded.Select(entry => entry.Id).Order(), seen.Order());
    }

    [Fact]
    public async Task GoingBack_ReturnsTheSamePageThatWasLeft()
    {
        await SeedAsync();
        await using var context = new PancrasDbContext(_options);
        var handler = Handler(context);

        var first = await handler.HandleAsync(new AuditQuery(), null, false, 3, true, default);
        var second = await handler.HandleAsync(new AuditQuery(), first.Next, false, 3, true, default);
        var back = await handler.HandleAsync(new AuditQuery(), second.Previous, true, 3, true, default);

        Assert.Equal(first.Entries.Select(entry => entry.Id), back.Entries.Select(entry => entry.Id));
        Assert.Null(back.Previous);
        Assert.NotNull(back.Next);
    }

    [Fact]
    public async Task FirstPage_HasNoPrevious_AndTheLastHasNoNext()
    {
        await SeedAsync();
        await using var context = new PancrasDbContext(_options);
        var handler = Handler(context);

        var first = await handler.HandleAsync(new AuditQuery(), null, false, 3, true, default);
        var second = await handler.HandleAsync(new AuditQuery(), first.Next, false, 3, true, default);
        var third = await handler.HandleAsync(new AuditQuery(), second.Next, false, 3, true, default);

        Assert.Null(first.Previous);
        Assert.NotNull(first.Next);
        Assert.NotNull(second.Previous);
        Assert.Single(third.Entries);
        Assert.Null(third.Next);
        Assert.NotNull(third.Previous);
    }

    [Fact]
    public async Task ABrokenCursor_FallsBackToTheFirstPage()
    {
        await SeedAsync();
        await using var context = new PancrasDbContext(_options);
        var handler = Handler(context);

        var page = await handler.HandleAsync(new AuditQuery(), "khong-phai-dau-trang", true, 3, true, default);

        Assert.Equal(3, page.Entries.Count);
        Assert.Null(page.Previous);
    }

    [Fact]
    public async Task EmptyLog_ReturnsAnEmptyPage()
    {
        await using var context = new PancrasDbContext(_options);

        var page = await Handler(context).HandleAsync(new AuditQuery(), null, false, 20, true, default);

        Assert.Empty(page.Entries);
        Assert.Null(page.Next);
        Assert.Null(page.Previous);
    }

    [Fact]
    public void Cursor_RoundTripsThroughItsToken()
    {
        var cursor = new AuditCursor(Start, Guid.CreateVersion7());

        var parsed = AuditCursor.TryParse(cursor.Encode());

        Assert.Equal(cursor, parsed);
        Assert.Null(AuditCursor.TryParse(null));
        Assert.Null(AuditCursor.TryParse(new string('x', 200)));
    }

    [Fact]
    public void TheCursorCondition_TranslatesToSqlForPostgres()
    {
        var options = new DbContextOptionsBuilder<PancrasDbContext>()
            .UseNpgsql("Host=localhost;Database=none;Username=none;Password=none")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new PancrasDbContext(options);
        var cursor = new AuditCursor(Start, Guid.CreateVersion7());

        var sql = AuditLog.ApplyCursor(context.AuditEntries.AsNoTracking(), cursor, false, true).ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("OFFSET", sql, StringComparison.Ordinal);
    }
}
