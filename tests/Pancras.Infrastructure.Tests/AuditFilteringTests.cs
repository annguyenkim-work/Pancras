using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

public class AuditFilteringTests
{
    private readonly DbContextOptions<PancrasDbContext> _options = new DbContextOptionsBuilder<PancrasDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    private async Task SeedAsync()
    {
        // 2026-11-15 20:00 UTC = 03:00 ngày 16/11 giờ Việt Nam; 2026-11-16 10:00 UTC = 17:00 ngày 16/11.
        await using var context = new PancrasDbContext(_options);
        context.AuditEntries.AddRange(
            AuditEntry.Create(new DateTimeOffset(2026, 11, 15, 20, 0, 0, TimeSpan.Zero), null, "an", AuditActions.LoginSucceeded),
            AuditEntry.Create(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero), null, "binh", AuditActions.PricesChanged, "catalog_item", "x"),
            AuditEntry.Create(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero), null, "an", AuditActions.PricesChanged, "catalog_item", "y"));
        await context.SaveChangesAsync();
    }

    private AuditLog Log(PancrasDbContext context) => new(context, NullLogger<AuditLog>.Instance);

    [Fact]
    public async Task FiltersByActor_Action_Target_AndVietnameseDayRange()
    {
        await SeedAsync();
        await using var context = new PancrasDbContext(_options);
        var log = Log(context);

        var byActor = await log.ListPageAsync(new AuditQuery(Actor: "an"), null, false, 20, true, default);
        var byAction = await log.ListPageAsync(new AuditQuery(Action: AuditActions.PricesChanged), null, false, 20, true, default);
        var byTarget = await log.ListPageAsync(new AuditQuery(Target: "catalog_item"), null, false, 20, true, default);
        var onTheSixteenth = await log.ListPageAsync(new AuditQuery(From: new DateOnly(2026, 11, 16), To: new DateOnly(2026, 11, 16)), null, false, 20, false, default);

        Assert.Equal(2, byActor.Count);
        Assert.Equal(2, byAction.Count);
        Assert.Equal(2, byTarget.Count);
        Assert.Equal(["an", "binh"], onTheSixteenth.Select(entry => entry.ActorCode));
    }

    [Fact]
    public async Task CombinesFilters_WithTheFreeTextSearch()
    {
        await SeedAsync();
        await using var context = new PancrasDbContext(_options);

        var found = await Log(context).ListPageAsync(new AuditQuery(Search: "catalog", Actor: "an"), null, false, 20, true, default);

        Assert.Single(found);
    }
}
