using Pancras.Application.Inventory;
using Pancras.Domain.Notifications;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Notifications;

public sealed class ListNotificationsHandler(IInventoryRepository repository)
{
    public Task<IReadOnlyList<Notification>> HandleAsync(Guid employeeId, CancellationToken cancellationToken) =>
        repository.ListNoticesAsync(employeeId, cancellationToken);

    public async Task<int> CountUnseenAsync(Guid employeeId, CancellationToken cancellationToken) =>
        (await repository.ListNoticesAsync(employeeId, cancellationToken)).Count(notice => !notice.Seen);

    public async Task<Notification?> OpenAsync(Guid noticeId, Guid employeeId, CancellationToken cancellationToken)
    {
        var notice = await repository.FindNoticeForUpdateAsync(noticeId, cancellationToken);
        if (notice is null || notice.RecipientEmployeeId != employeeId)
        {
            return null;
        }

        notice.MarkSeen();
        await repository.CommitAsync(cancellationToken);
        return notice;
    }
}
