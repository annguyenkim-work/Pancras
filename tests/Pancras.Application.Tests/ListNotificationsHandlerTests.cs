using Pancras.Application.Notifications;
using Pancras.Domain.Notifications;

namespace Pancras.Application.Tests;

public class ListNotificationsHandlerTests
{
    private static (ListNotificationsHandler Handler, Guid Me, FakeInventoryStore Inventory) Arrange(int count)
    {
        var inventory = new FakeInventoryStore();
        var me = Guid.CreateVersion7();
        for (var i = 0; i < count; i++)
        {
            inventory.Notices.Add(Notification.Create(me, null, Guid.CreateVersion7(), NotificationKind.Purchase, "step", new DateTimeOffset(2026, 11, 1, 8, i, 0, TimeSpan.Zero)));
        }

        return (new ListNotificationsHandler(inventory), me, inventory);
    }

    [Fact]
    public async Task APageBeyondTheEnd_FallsBackToTheLastPage()
    {
        var (handler, me, _) = Arrange(5);

        var page = await handler.HandleAsync(me, 9, 2, default);

        Assert.Equal(3, page.PageNumber);
        Assert.Single(page.Items);
        Assert.Equal(5, page.Total);
    }

    [Fact]
    public async Task APageBelowOne_IsTheFirstPage_AndNoNoticesGivesPageOne()
    {
        var (handler, me, _) = Arrange(0);

        var page = await handler.HandleAsync(me, 0, 20, default);

        Assert.Equal(1, page.PageNumber);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task UnseenCount_IgnoresSeenNotices()
    {
        var (handler, me, inventory) = Arrange(3);
        inventory.Notices[0].MarkSeen();

        Assert.Equal(2, await handler.CountUnseenAsync(me, default));
    }
}
