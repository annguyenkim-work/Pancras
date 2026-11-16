using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Domain.Notifications;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Phân trang và đếm chưa xem làm ở truy vấn (docs/03-ui-ux/components.md mục 6), chỉ của người nhận; sắp theo thời điểm tạo, mới nhất trước, không đổi chỗ khi đã xem.</summary>
public class NoticePagingTests
{
    private readonly DbContextOptions<PancrasDbContext> _options = new DbContextOptionsBuilder<PancrasDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private async Task<(Guid Mine, Guid Other)> SeedAsync()
    {
        var mine = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await using var context = new PancrasDbContext(_options);
        for (var i = 0; i < 5; i++)
        {
            var notice = Notification.Create(mine, null, Guid.CreateVersion7(), NotificationKind.Purchase, "step" + i, Start.AddMinutes(i));
            if (i is 3 or 4)
            {
                notice.MarkSeen();
            }

            context.Notifications.Add(notice);
        }

        context.Notifications.Add(Notification.Create(other, null, Guid.CreateVersion7(), NotificationKind.Purchase, "other", Start));
        await context.SaveChangesAsync();
        return (mine, other);
    }

    private static InventoryRepository Repository(PancrasDbContext context) =>
        new(context, NullLogger<InventoryRepository>.Instance);

    [Fact]
    public async Task Pages_AreCutByTheDatabase_NewestFirstWhetherSeenOrNot_AndOnlyForTheRecipient()
    {
        var (mine, _) = await SeedAsync();
        await using var context = new PancrasDbContext(_options);
        var repository = Repository(context);

        var first = await repository.ListNoticesPageAsync(mine, 1, 2, default);
        var second = await repository.ListNoticesPageAsync(mine, 2, 2, default);
        var third = await repository.ListNoticesPageAsync(mine, 3, 2, default);

        Assert.Equal(5, first.Total);
        Assert.Equal(["step4", "step3"], first.Items.Select(notice => notice.Step));
        Assert.Equal(["step2", "step1"], second.Items.Select(notice => notice.Step));
        Assert.Equal(["step0"], third.Items.Select(notice => notice.Step));
    }

    [Fact]
    public async Task APageBeyondTheEnd_IsEmpty_ButStillReportsTheTotal()
    {
        var (mine, _) = await SeedAsync();
        await using var context = new PancrasDbContext(_options);

        var page = await Repository(context).ListNoticesPageAsync(mine, 9, 2, default);

        Assert.Empty(page.Items);
        Assert.Equal(5, page.Total);
    }

    [Fact]
    public async Task UnseenCount_CountsOnlyMyUnseenNotices()
    {
        var (mine, other) = await SeedAsync();
        await using var context = new PancrasDbContext(_options);
        var repository = Repository(context);

        Assert.Equal(3, await repository.CountUnseenNoticesAsync(mine, default));
        Assert.Equal(1, await repository.CountUnseenNoticesAsync(other, default));
        Assert.Equal(0, await repository.CountUnseenNoticesAsync(Guid.CreateVersion7(), default));
    }
}
